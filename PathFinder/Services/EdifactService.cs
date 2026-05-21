using PathFinder.Models;

namespace PathFinder.Services;

internal static class EdifactService
{
    /// <summary>
    /// Formats EDIFACT text by ensuring each segment (terminated by a non-escaped single quote)
    /// appears on its own line. Existing line breaks are removed and re-inserted after each
    /// unescaped segment terminator <c>'</c>.
    /// </summary>
    public static string FormatEdi(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool inRelease = false;

        foreach (char c in text)
        {
            if (inRelease)
            {
                sb.Append(c);
                inRelease = false;
            }
            else if (c == '?')
            {
                sb.Append(c);
                inRelease = true;
            }
            else if (c == '\'')
            {
                sb.Append(c);
                sb.Append('\n');
            }
            else if (c is not '\r' and not '\n')
            {
                sb.Append(c);
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Minifies EDIFACT text by removing all line breaks, producing a single-line output.
    /// Preserves all segment content and escaped characters.
    /// </summary>
    public static string MinifyEdi(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is not '\r' and not '\n')
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Validates EDIFACT structure. Returns <c>null</c> when the text is valid;
    /// otherwise returns a human-readable error description listing all problems found,
    /// each with the segment number and specific issue.
    /// </summary>
    public static string? ValidateEdi(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Document is empty — expected at least one EDIFACT segment.";

        var formatted = FormatEdi(text);
        if (string.IsNullOrWhiteSpace(formatted))
            return "No EDIFACT segments found — the document contains no unescaped segment terminator (').";

        var lines = formatted.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return "No EDIFACT segments found — the document contains no unescaped segment terminator (').";

        var errors = new System.Collections.Generic.List<string>();

        // Check that every non-empty raw line ends with an unescaped segment terminator
        var rawLines = text.Split('\n');
        for (int r = 0; r < rawLines.Length; r++)
        {
            var raw = rawLines[r].TrimEnd('\r', ' ', '\t');
            if (raw.Length == 0) continue;
            if (!EndsWithUnescapedTerminator(raw))
                errors.Add($"Line {r + 1}: does not end with a segment terminator (') — \"{Truncate(raw, 40)}\".");
        }

        // Check for trailing content after the last segment terminator
        var trimmed = text.Replace("\r", "").Replace("\n", "");
        if (trimmed.Length > 0)
        {
            bool inRelease = false;
            int lastTerminator = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (inRelease) { inRelease = false; continue; }
                if (trimmed[i] == '?') { inRelease = true; continue; }
                if (trimmed[i] == '\'') lastTerminator = i;
            }

            if (lastTerminator < 0)
            {
                return "No segment terminator (') found — every EDIFACT segment must end with an unescaped single quote.";
            }

            var trailing = trimmed[(lastTerminator + 1)..].Trim();
            if (trailing.Length > 0)
                errors.Add($"Unexpected content after last segment terminator: \"{Truncate(trailing, 40)}\"");
        }

        // ── per-segment structure validation ───────────────────────────
        string? unhRef = null;       // reference number from the current UNH
        int unhSegNum = 0;           // 1-based position of the current UNH
        int segsSinceUnh = 0;        // count of segments from UNH to UNT (inclusive)

        for (int i = 0; i < lines.Length; i++)
        {
            var segment = lines[i].TrimEnd();
            if (segment.Length == 0) continue;

            int segNum = i + 1;

            // Each segment must end with an unescaped terminator '
            if (segment[^1] != '\'')
            {
                errors.Add($"Segment {segNum}: missing segment terminator (') at end of \"{Truncate(segment, 40)}\".");
                continue;
            }

            // Empty segment (just a terminator)
            if (segment.Length == 1)
            {
                errors.Add($"Segment {segNum}: empty segment — expected a segment tag before the terminator.");
                continue;
            }

            // Segment tag: 2-3 uppercase letters before the first separator (+, :, or ')
            int tagEnd = 0;
            while (tagEnd < segment.Length && char.IsUpper(segment[tagEnd]))
                tagEnd++;

            if (tagEnd == 0)
            {
                errors.Add($"Segment {segNum}: segment tag must start with uppercase letters, found '{segment[0]}' in \"{Truncate(segment, 40)}\".");
                continue;
            }

            if (tagEnd == 1)
            {
                errors.Add($"Segment {segNum}: segment tag '{segment[..tagEnd]}' is too short — tags must be 2–3 uppercase letters.");
                continue;
            }

            if (tagEnd > 3)
            {
                errors.Add($"Segment {segNum}: segment tag '{segment[..System.Math.Min(tagEnd, 6)]}' is too long — tags must be 2–3 uppercase letters.");
                continue;
            }

            if (tagEnd < segment.Length && segment[tagEnd] is not '+' and not ':' and not '\'')
            {
                errors.Add($"Segment {segNum}: unexpected character '{segment[tagEnd]}' after tag '{segment[..tagEnd]}' — expected '+', ':', or segment terminator.");
                continue;
            }

            var tag = segment[..tagEnd];
            if (unhRef is not null) segsSinceUnh++;

            // ── UNH / UNT pairing ──────────────────────────────────────
            if (tag == "UNH")
            {
                if (unhRef is not null)
                    errors.Add($"Segment {segNum}: found a second UNH before closing the previous one (UNH at segment {unhSegNum}, reference \"{unhRef}\").");

                var elements = SplitElements(segment);
                unhRef = elements.Length > 1 ? elements[1] : null;
                unhSegNum = segNum;
                segsSinceUnh = 1; // UNH itself counts
            }
            else if (tag == "UNT")
            {
                var elements = SplitElements(segment);
                var untCountStr = elements.Length > 1 ? elements[1] : null;
                var untRef = elements.Length > 2 ? elements[2] : null;

                if (unhRef is null)
                {
                    errors.Add($"Segment {segNum}: UNT found without a preceding UNH.");
                }
                else
                {
                    // Validate reference number match
                    if (untRef is not null && untRef != unhRef)
                        errors.Add($"Segment {segNum}: UNT reference number \"{untRef}\" does not match UNH reference number \"{unhRef}\" (UNH at segment {unhSegNum}).");

                    // Validate segment count
                    if (untCountStr is not null && int.TryParse(untCountStr, out int declaredCount) && declaredCount != segsSinceUnh)
                        errors.Add($"Segment {segNum}: UNT declares {declaredCount} segments but the actual count from UNH to UNT is {segsSinceUnh}.");
                }

                unhRef = null; // close the message
            }
        }

        // Unclosed UNH
        if (unhRef is not null)
            errors.Add($"UNH at segment {unhSegNum} (reference \"{unhRef}\") has no matching UNT.");

        return errors.Count > 0 ? string.Join("\n", errors) : null;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    /// <summary>
    /// Returns <c>true</c> if <paramref name="line"/> ends with an unescaped segment
    /// terminator (<c>'</c>). Correctly handles the <c>?</c> release character.
    /// </summary>
    private static bool EndsWithUnescapedTerminator(string line)
    {
        if (line.Length == 0 || line[^1] != '\'') return false;

        // Count consecutive '?' immediately before the final quote
        int escapes = 0;
        for (int i = line.Length - 2; i >= 0 && line[i] == '?'; i--)
            escapes++;

        // If even number of '?' precede the quote, it's unescaped (each ?? is a literal ?)
        return escapes % 2 == 0;
    }

    /// <summary>
    /// Splits an EDIFACT segment into its data elements by unescaped <c>+</c> separators.
    /// The segment terminator <c>'</c> is stripped. The first element is the segment tag.
    /// </summary>
    private static string[] SplitElements(string segment)
    {
        // Strip trailing terminator
        var body = segment.EndsWith('\'') ? segment[..^1] : segment;

        var elements = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        bool inRelease = false;

        foreach (char c in body)
        {
            if (inRelease)
            {
                current.Append(c);
                inRelease = false;
            }
            else if (c == '?')
            {
                current.Append(c);
                inRelease = true;
            }
            else if (c == '+')
            {
                elements.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        elements.Add(current.ToString());
        return elements.ToArray();
    }

    // ── Definition-aware validation ──────────────────────────────────────────

    /// <summary>
    /// Validates the EDIFACT text against the bundled directory definitions
    /// (segment structure, field constraints, code lists).
    /// Returns an empty list when the message is valid or when no definition
    /// can be found for the message type.
    /// Each error string begins with "Line N:" for clickable navigation.
    /// </summary>
    public static IReadOnlyList<string> ValidateEdiDefinition(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var formatted = FormatEdi(text);
        var lines = formatted.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return [];

        // ── 1. Parse all segments with line numbers ──────────────────────────
        var segmentsWithLines = BuildSegmentLineMap(text, lines);
        var errors = new List<string>();

        // ── 2. Always validate UNA format (independent of definitions) ────────
        ValidateUnaFormat(segmentsWithLines, errors);

        // ── 3. Identify directory and message type from UNH ──────────────────
        var unh = segmentsWithLines.FirstOrDefault(s => s.Tag == "UNH");
        if (unh == default)
        {
            errors.Add("Missing UNH segment — cannot determine EDIFACT message type.");
            return errors;
        }

        if (!TryParseUnh(unh.Raw, out var directory, out var messageType))
        {
            errors.Add($"Line {unh.LineNumber}: Cannot parse UNH S009 identifier to determine directory and message type.");
            return errors;
        }

        // ── 4. Check directory and message type exist in definitions ─────────
        if (!EdifactDefinitionService.DirectoryKnown(directory))
        {
            errors.Add(
                $"Line {unh.LineNumber}: EDIFACT directory '{directory}' is not in the bundled " +
                $"definitions. Supported directories: D96A, D97A, D98A, D99A, D99B, D01B, D10B.");
            return errors;
        }

        var msgDef = EdifactDefinitionService.LookupMessage(directory, messageType);
        if (msgDef is null)
        {
            errors.Add(
                $"Line {unh.LineNumber}: Message type '{messageType}' was not found in directory " +
                $"'{directory}' definitions.");
            return errors;
        }

        // ── 4. Isolate message body (exclusive of UNH and UNT) ───────────────
        var messageBody = segmentsWithLines
            .Where(s => s.Tag is not "UNA" and not "UNB" and not "UNG"
                                           and not "UNE" and not "UNH"
                                           and not "UNT" and not "UNZ")
            .ToList();

        // ── 5. Structural validation ──────────────────────────────────────────
        // edifactory.de includes UNH and UNT as the first/last items in every
        // message structure definition. Strip them from the top-level structure
        // before running the validator — they are already handled separately.
        var bodyStructure = msgDef.Structure
            .Where(item => !(item.Kind == "segment" &&
                             item.Tag is "UNA" or "UNB" or "UNG" or "UNE"
                                              or "UNH" or "UNT" or "UNZ"))
            .ToList();
        var bodyMsgDef = new EdifactMessageDef { Structure = bodyStructure };
        var structureErrors = EdifactStructuralValidator.ValidateStructure(messageBody, bodyMsgDef);
        errors.AddRange(structureErrors);

        // ── 6. Envelope segment field validation (UNA, UNB, UNH, UNT, UNZ) ───
        ValidateEnvelopeSegments(segmentsWithLines, directory, errors);

        // ── 7. Per-segment field validation for message body ─────────────────
        foreach (var (tag, raw, lineNumber) in messageBody)
        {
            var segDef = EdifactDefinitionService.LookupSegment(directory, tag);
            if (segDef is null)
                continue;   // No definition → skip field validation for this segment

            foreach (var err in EdifactFieldValidator.ValidateFields(raw, lineNumber, segDef, directory))
                errors.Add(err);
        }

        return errors;
    }

    // ── Segment → line-number mapping ────────────────────────────────────────

    internal record struct SegmentEntry(string Tag, string Raw, int LineNumber);

    /// <summary>
    /// Builds a list of (Tag, Raw, OriginalLineNumber) for every segment in
    /// <paramref name="formattedLines"/>, mapping each back to its 1-based line
    /// number in the original (unformatted) text.
    /// </summary>
    private static List<SegmentEntry> BuildSegmentLineMap(string originalText, string[] formattedLines)
    {
        // Strategy: we need the line numbers from the FORMATTED text because each
        // formatted line IS one segment.  However, the original text may have had
        // all segments on one line.  The Messages-panel line navigation should
        // point to the actual line in the file as the user sees it.
        // We use the structured formatter and then re-scan the original to find where
        // each segment tag first appears as a new segment start.

        var result = new List<SegmentEntry>();

        // Build a quick-access mapping: originalLine → segment tag that starts on it.
        var originalLines = originalText.Replace("\r\n", "\n").Replace("\r", "\n")
                                        .Split('\n');

        // Track which segment we're on in the original file.
        // If the original file has one segment per line (already formatted), the
        // 1-based index of formattedLines[i] maps exactly to the line in the file.
        // If all on one line, all segments map to line 1.

        // Simple heuristic: formatted has segments, original has lines.  For each
        // segment in formattedLines, scan forward in originalLines from the last
        // matched position to find its tag.
        int origIdx = 0;

        for (int fi = 0; fi < formattedLines.Length; fi++)
        {
            var seg = formattedLines[fi].TrimEnd();
            if (seg.Length == 0) continue;

            // Extract tag (letters at start)
            int tagEnd = 0;
            while (tagEnd < seg.Length && char.IsUpper(seg[tagEnd])) tagEnd++;
            var tag = seg[..tagEnd];
            if (tag.Length < 2) continue;

            // Find this tag in the original file starting from origIdx
            int foundLine = origIdx + 1; // default: last known position
            for (int oi = origIdx; oi < originalLines.Length; oi++)
            {
                var ol = originalLines[oi];
                int ti = 0;
                // Skip whitespace at start
                while (ti < ol.Length && ol[ti] == ' ') ti++;
                // Check if tag appears at this position
                if (ol.Length >= ti + tag.Length &&
                    string.Compare(ol, ti, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                    (ol.Length == ti + tag.Length || ol[ti + tag.Length] is '+' or ':' or '\''))
                {
                    foundLine = oi + 1;
                    origIdx = oi; // don't advance past here; next segment may be on same line
                    break;
                }
            }

            result.Add(new SegmentEntry(tag, seg, foundLine));
        }

        return result;
    }

    // ── UNH S009 parsing ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the EDIFACT directory code (e.g. "D96A") and message type
    /// (e.g. "IFTMCS") from an UNH segment.
    /// UNH format: UNH+ref+msgType:version:release:controllingAgency'
    /// S009 composite:  0065:msgType  0052:version  0054:release  0051:agency
    /// Example:         UNH+1+IFTMCS:D:96A:UN'
    /// </summary>
    private static bool TryParseUnh(string unh, out string directory, out string messageType)
    {
        directory = "";
        messageType = "";

        var elements = SplitElements(unh);
        if (elements.Length < 3) return false;

        // elements[2] = S009 composite: "IFTMCS:D:96A:UN"
        var s009 = elements[2].Split(':');
        if (s009.Length < 3) return false;

        messageType = s009[0].Trim().ToUpperInvariant();        // e.g. "IFTMCS"
        var version = s009[1].Trim().ToUpperInvariant();         // e.g. "D"
        var release = s009[2].Trim().ToUpperInvariant();         // e.g. "96A"

        // Directory = version + release, e.g. "D" + "96A" = "D96A"
        directory = version + release;

        return messageType.Length >= 2 && directory.Length >= 2;
    }

    // ── UNA format validation (independent of definitions) ───────────────────

    private static void ValidateUnaFormat(List<SegmentEntry> segments, List<string> errors)
    {
        foreach (var (tag, raw, lineNumber) in segments)
        {
            if (tag != "UNA") continue;

            // UNA = "UNA" (3 chars) + 5 delimiter chars + 1 segment terminator = 9 chars total.
            // After stripping the trailing terminator, the body should be 8 chars.
            var body = raw.EndsWith('\'') ? raw[..^1] : raw;
            if (body.Length != 8 || !body.StartsWith("UNA", StringComparison.OrdinalIgnoreCase))
                errors.Add($"Line {lineNumber}: UNA segment must be exactly 9 characters " +
                           $"('UNA' + 5 delimiter characters + segment terminator character).");
        }
    }

    // ── Envelope field validation ─────────────────────────────────────────────

    private static void ValidateEnvelopeSegments(
        IEnumerable<SegmentEntry> allSegments,
        string directory,
        List<string> errors)
    {
        foreach (var (tag, raw, lineNumber) in allSegments)
        {
            // UNA is handled separately by ValidateUnaFormat
            if (tag is "UNA" or not ("UNB" or "UNG" or "UNE" or "UNH" or "UNT" or "UNZ"))
                continue;

            var segDef = EdifactDefinitionService.LookupSegment(directory, tag);
            if (segDef is null) continue;

            foreach (var err in EdifactFieldValidator.ValidateFields(raw, lineNumber, segDef, directory))
                errors.Add(err);
        }
    }
}

