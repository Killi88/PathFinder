using PathFinder.Models;

namespace PathFinder.Services;

/// <summary>
/// Validates the structure of an EDIFACT message against its definition:
/// segment presence, order, and group occurrence counts.
/// All errors include "Line N:" prefixes so the caller (ParseValidationErrors)
/// can make them clickable in the Messages panel.
/// </summary>
internal static class EdifactStructuralValidator
{
    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Validates message segments (between UNH and UNT, exclusive) against
    /// <paramref name="def"/>.  Returns one error string per problem found;
    /// each string begins with "Line N:".
    /// </summary>
    internal static IReadOnlyList<string> ValidateStructure(
        IReadOnlyList<EdifactService.SegmentEntry> messageSegments,
        EdifactMessageDef def)
    {
        var errors = new List<string>();
        var state = new StructureState(def.Structure, errors);

        foreach (var seg in messageSegments)
            state.Process(seg.Tag, seg.LineNumber);

        // Close any remaining open groups and check for un-met mandatory items
        state.CloseAll();

        return errors;
    }

    // ── Internal State Machine ────────────────────────────────────────────────

    /// <summary>
    /// Tracks one level of parsing context: the list of structure items at
    /// this level, how far through them we've advanced, and how many times
    /// each item has been seen.
    /// </summary>
    private sealed class GroupCursor
    {
        internal IReadOnlyList<EdifactStructureItem> Items { get; }

        /// <summary>Next position to scan from when looking for a segment.</summary>
        internal int Position { get; set; }

        /// <summary>
        /// Occurrence count per item index (segment or group).
        /// Key = index in Items.
        /// </summary>
        private readonly Dictionary<int, int> _counts = [];

        internal GroupCursor(IReadOnlyList<EdifactStructureItem> items)
        {
            Items = items;
        }

        internal int GetCount(int index)
            => _counts.TryGetValue(index, out var c) ? c : 0;

        internal void Increment(int index)
            => _counts[index] = GetCount(index) + 1;
    }

    private sealed class StructureState
    {
        private readonly List<string> _errors;
        private readonly Stack<GroupCursor> _stack = new();

        internal StructureState(IReadOnlyList<EdifactStructureItem> rootItems, List<string> errors)
        {
            _errors = errors;
            _stack.Push(new GroupCursor(rootItems));
        }

        // ── Process one segment tag ───────────────────────────────────────────

        internal void Process(string tag, int lineNumber)
        {
            // Try to place the tag in the current context or a parent context.
            // We allow popping up to find the right level (closing optional groups
            // along the way, or reporting missed mandatory items when closing them).
            while (_stack.Count > 0)
            {
                var cursor = _stack.Peek();
                var result = TryPlaceInCursor(cursor, tag, lineNumber);

                if (result == PlaceResult.Placed)
                    return;

                if (result == PlaceResult.NewGroupRepetition)
                    return;

                // Could not place in this cursor — close it and try parent.
                // Before popping, check for un-met mandatory items remaining in
                // this cursor's item list (after the current position).
                ReportRemainingMandatory(cursor, lineNumber, closing: true);
                _stack.Pop();
            }

            // Stack exhausted — segment could not be placed anywhere.
            _errors.Add($"Line {lineNumber}: Unexpected segment '{tag}' — not expected at this point in the {DirectionHint(tag)} message structure.");
        }

        // ── Close all open groups at end of message ───────────────────────────

        internal void CloseAll()
        {
            while (_stack.Count > 0)
            {
                var cursor = _stack.Pop();
                ReportRemainingMandatory(cursor, lineNumber: 0, closing: false);
            }
        }

        // ── Attempt to place a tag in the given cursor ────────────────────────

        private enum PlaceResult { Placed, NewGroupRepetition, NotFound }

        private PlaceResult TryPlaceInCursor(GroupCursor cursor, string tag, int lineNumber)
        {
            var items = cursor.Items;

            // ── Check positions already passed for a segment at max ───────────
            // When a segment reaches its MaxOccurrences the cursor advances past
            // it.  If the same tag appears again, the forward scan below will not
            // see it.  Detect this and report a clear "exceeds max" error instead
            // of falling through to "Unexpected segment".
            // Skip position 0 in non-root cursors: that position is the group's
            // trigger segment.  When the trigger appears again the correct action
            // is to pop this group and start a new repetition in the parent.
            int backStart = _stack.Count > 1 ? 1 : 0;
            for (int j = backStart; j < cursor.Position; j++)
            {
                var prev = items[j];
                if (prev.Kind == "segment" &&
                    string.Equals(prev.Tag, tag, StringComparison.OrdinalIgnoreCase) &&
                    cursor.GetCount(j) >= prev.MaxOccurrences)
                {
                    _errors.Add($"Line {lineNumber}: Segment '{tag}' exceeds its maximum occurrence count ({prev.MaxOccurrences}).");
                    return PlaceResult.Placed; // absorb error without disturbing cursor state
                }
            }

            // ── Scan forward from current position ────────────────────────────
            for (int i = cursor.Position; i < items.Count; i++)
            {
                var item = items[i];

                if (item.Kind == "segment")
                {
                    if (!string.Equals(item.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        // Skipping over this segment — if mandatory, that's an error.
                        if (item.Mandatory && cursor.GetCount(i) == 0)
                            _errors.Add($"Line {lineNumber}: Missing mandatory segment '{item.Tag}' which must appear before '{tag}'.");
                        continue;
                    }

                    // Tag matches
                    if (cursor.GetCount(i) >= item.MaxOccurrences)
                    {
                        _errors.Add($"Line {lineNumber}: Segment '{tag}' exceeds its maximum occurrence count ({item.MaxOccurrences}).");
                        cursor.Position = i; // stay at same position
                        return PlaceResult.Placed; // count the error but keep going
                    }

                    cursor.Increment(i);
                    // Keep the cursor AT this position when more occurrences are still
                    // allowed, so repeated consecutive occurrences (e.g. FTX max=9) can
                    // all be placed without scanning past this slot.  Only advance past
                    // the slot once the maximum has been reached.
                    cursor.Position = cursor.GetCount(i) >= item.MaxOccurrences ? i + 1 : i;
                    return PlaceResult.Placed;
                }

                if (item.Kind == "group")
                {
                    var groupItems = item.Items ?? [];

                    // Check whether this tag is the "trigger" for this group
                    // (i.e. it could be the first segment inside the group).
                    if (!GroupCanStart(groupItems, tag))
                    {
                        // If we're skipping this group before it was ever seen, and it's
                        // mandatory, that's an error (unless it's optional, which is fine).
                        if (item.Mandatory && cursor.GetCount(i) == 0)
                            _errors.Add($"Line {lineNumber}: Missing mandatory segment group '{item.Name}' (expected before '{tag}').");
                        continue;
                    }

                    // The group can start with this tag.
                    if (cursor.GetCount(i) >= item.MaxOccurrences)
                    {
                        // Group at max repetitions — cannot start a new repetition.
                        // Continue scanning to see if another segment/group matches.
                        continue;
                    }

                    // Advance to this group's position in parent and enter the group.
                    cursor.Position = i; // stay here for potential further repetitions
                    cursor.Increment(i);
                    var child = new GroupCursor(groupItems);
                    _stack.Push(child);

                    // Now process the tag within the new child cursor.
                    TryPlaceInCursor(child, tag, lineNumber);
                    return PlaceResult.Placed;
                }
            }

            // Not found via forward scan — check if we're already AT position 0
            // and this tag is the trigger for the current GROUP itself (repetition).
            // (Only relevant when this cursor is a child group, not root).
            if (_stack.Count > 1 && cursor.Position > 0)
            {
                // We are inside a group and need to see if this segment restarts it.
                // The parent decides — just signal NotFound so the caller pops us.
            }

            return PlaceResult.NotFound;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the given tag could open (i.e. is the trigger for)
        /// a segment group — i.e. when it matches the first SEGMENT in the group
        /// (recursing into nested groups if the group starts with a nested group).
        /// </summary>
        private static bool GroupCanStart(IReadOnlyList<EdifactStructureItem> items, string tag)
            => EdifactStructuralValidator.GroupCanStart(items, tag);

        private void ReportRemainingMandatory(GroupCursor cursor, int lineNumber, bool closing)
        {
            for (int i = cursor.Position; i < cursor.Items.Count; i++)
            {
                var item = cursor.Items[i];
                if (!item.Mandatory || cursor.GetCount(i) > 0)
                    continue;

                string name = item.Kind == "segment" ? $"segment '{item.Tag}'"
                                                     : $"segment group '{item.Name}'";
                if (closing)
                    _errors.Add($"Line {lineNumber}: Mandatory {name} was not found in the expected position.");
                else
                    _errors.Add($"Missing mandatory {name} at the end of the message.");
            }
        }

        private static string DirectionHint(string tag) => tag switch
        {
            "UNA" or "UNB" or "UNG" or "UNE" or "UNH" or "UNT" or "UNZ"
                => "service envelope",
            _ => "body"
        };
    }

    // ── Group Folding ─────────────────────────────────────────────────────────

    /// <summary>
    /// Walks the message segments through the definition structure and returns
    /// fold regions for every segment group encountered.  Each region contains
    /// the group name (e.g. "SG1"), the start line of the first segment in the
    /// group, and the end line of the last segment in the group.
    /// </summary>
    internal static IReadOnlyList<(string GroupName, int StartLine, int EndLine)> GetGroupFoldings(
        IReadOnlyList<EdifactService.SegmentEntry> messageSegments,
        EdifactMessageDef def)
    {
        var regions = new List<(string, int, int)>();
        var tracker = new FoldingTracker(def.Structure, regions);
        foreach (var seg in messageSegments)
            tracker.Process(seg.Tag, seg.LineNumber);
        tracker.CloseAll();
        return regions;
    }

    // ── Shared helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="tag"/> could start (trigger) the given
    /// group item list — i.e. it matches the first segment in the group,
    /// recursing into nested groups if the first item is itself a group.
    /// </summary>
    private static bool GroupCanStart(IReadOnlyList<EdifactStructureItem> items, string tag)
    {
        if (items.Count == 0) return false;
        var first = items[0];
        if (first.Kind == "segment")
            return string.Equals(first.Tag, tag, StringComparison.OrdinalIgnoreCase);
        return first.Kind == "group" && GroupCanStart(first.Items ?? [], tag);
    }

    // ── FoldingTracker ────────────────────────────────────────────────────────

    private sealed class FoldingFrame(IReadOnlyList<EdifactStructureItem> items, string? groupName, int startLine)
    {
        public IReadOnlyList<EdifactStructureItem> Items { get; } = items;
        public string? GroupName { get; } = groupName;
        public int StartLine { get; } = startLine;
        public int LastLine { get; set; } = startLine;
        public int Position { get; set; }
        private readonly Dictionary<int, int> _counts = [];
        public int GetCount(int i) => _counts.TryGetValue(i, out var c) ? c : 0;
        public void Increment(int i) => _counts[i] = GetCount(i) + 1;
    }

    private sealed class FoldingTracker(
        IReadOnlyList<EdifactStructureItem> rootItems,
        List<(string, int, int)> regions)
    {
        private readonly Stack<FoldingFrame> _stack = new(
            [new FoldingFrame(rootItems, null, 0)]);

        public void Process(string tag, int lineNumber)
        {
            while (_stack.Count > 0)
            {
                var frame = _stack.Peek();
                if (TryPlace(frame, tag, lineNumber))
                {
                    // Update last-seen line for every ancestor frame too
                    foreach (var f in _stack) f.LastLine = lineNumber;
                    return;
                }
                // Can't place here — close this group
                _stack.Pop();
                if (frame.GroupName is not null && frame.LastLine > frame.StartLine)
                    regions.Add((frame.GroupName, frame.StartLine, frame.LastLine));
            }
        }

        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                var frame = _stack.Pop();
                if (frame.GroupName is not null && frame.LastLine > frame.StartLine)
                    regions.Add((frame.GroupName, frame.StartLine, frame.LastLine));
            }
        }

        private bool TryPlace(FoldingFrame frame, string tag, int lineNumber)
        {
            var items = frame.Items;
            for (int i = frame.Position; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Kind == "segment")
                {
                    if (!string.Equals(item.Tag, tag, StringComparison.OrdinalIgnoreCase))
                        continue;
                    frame.Increment(i);
                    frame.Position = frame.GetCount(i) >= item.MaxOccurrences ? i + 1 : i;
                    return true;
                }
                if (item.Kind == "group")
                {
                    var groupItems = item.Items ?? [];
                    if (!GroupCanStart(groupItems, tag)) continue;
                    if (frame.GetCount(i) >= item.MaxOccurrences) continue;
                    frame.Position = i;
                    frame.Increment(i);
                    var child = new FoldingFrame(groupItems, item.Name, lineNumber);
                    _stack.Push(child);
                    TryPlace(child, tag, lineNumber);
                    child.LastLine = lineNumber;
                    return true;
                }
            }
            return false;
        }
    }
}
