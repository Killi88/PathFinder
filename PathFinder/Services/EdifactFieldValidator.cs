using PathFinder.Models;

namespace PathFinder.Services;

/// <summary>
/// Validates the data-element values in a single EDIFACT segment against its
/// field definition.  Checks mandatory presence, maximum length, data type
/// (alphanumeric / numeric / alphabetic), and code-list membership for coded
/// elements.
/// </summary>
internal static class EdifactFieldValidator
{
    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Returns one error string per field-level problem in <paramref name="rawSegment"/>.
    /// Each string begins with "Line <paramref name="lineNumber"/>:".
    /// </summary>
    internal static IEnumerable<string> ValidateFields(
        string rawSegment,
        int lineNumber,
        EdifactSegmentDef def,
        string directory)
    {
        // Split the segment body (minus the trailing ') into data elements by +,
        // honouring the ? release character.
        var elements = SplitByUnescaped(rawSegment.TrimEnd('\''), '+');
        // elements[0] is the segment tag — skip it when matching against fields.

        var fieldDefs = def.Fields;
        int fieldIdx = 0;   // index into fieldDefs

        // elements[1..] correspond to fieldDefs[0..], with some elements possibly
        // absent (empty string) to represent omitted optional fields.
        for (int elemIdx = 1; elemIdx < elements.Count || fieldIdx < fieldDefs.Count; fieldIdx++)
        {
            if (fieldIdx >= fieldDefs.Count)
            {
                // More elements in the segment than the definition has fields.
                _errors_buffer ??= new List<string>();
                _errors_buffer.Add(
                    $"Line {lineNumber}: Segment '{def.Tag}' has more data elements than expected " +
                    $"(element {elemIdx} is surplus).");
                break;
            }

            var fieldDef = fieldDefs[fieldIdx];
            var rawValue = elemIdx < elements.Count ? elements[elemIdx] : "";
            elemIdx++;

            if (fieldDef.IsComposite)
            {
                foreach (var err in ValidateComposite(rawValue, lineNumber, def.Tag, fieldDef, directory))
                    yield return err;
            }
            else
            {
                foreach (var err in ValidateSimple(rawValue, lineNumber, def.Tag, fieldDef, directory))
                    yield return err;
            }
        }

        if (_errors_buffer is { Count: > 0 })
        {
            foreach (var e in _errors_buffer)
                yield return e;
            _errors_buffer = null;
        }
    }

    // ── Composite field validation ────────────────────────────────────────────

    private static IEnumerable<string> ValidateComposite(
        string rawComposite,
        int lineNumber,
        string segTag,
        EdifactFieldDef fieldDef,
        string directory)
    {
        // If the whole composite is empty and it's mandatory, report it.
        if (rawComposite.Length == 0)
        {
            if (fieldDef.Mandatory)
                yield return $"Line {lineNumber}: Mandatory composite '{fieldDef.Id}' ({fieldDef.Name}) " +
                             $"is missing in segment '{segTag}'.";
            yield break;
        }

        if (fieldDef.Components is null || fieldDef.Components.Count == 0)
            yield break;

        var components = SplitByUnescaped(rawComposite, ':');

        for (int ci = 0; ci < fieldDef.Components.Count; ci++)
        {
            var compDef = fieldDef.Components[ci];
            var compVal = ci < components.Count ? components[ci] : "";

            foreach (var err in ValidateSimple(compVal, lineNumber, segTag, compDef, directory))
                yield return err;
        }

        // Extra components beyond what the definition expects
        if (components.Count > fieldDef.Components.Count)
        {
            yield return $"Line {lineNumber}: Composite '{fieldDef.Id}' in segment '{segTag}' " +
                         $"has {components.Count} components but the definition only expects " +
                         $"{fieldDef.Components.Count}.";
        }
    }

    // ── Simple (non-composite) field validation ───────────────────────────────

    private static IEnumerable<string> ValidateSimple(
        string value,
        int lineNumber,
        string segTag,
        EdifactFieldDef fieldDef,
        string directory)
    {
        bool isEmpty = value.Length == 0;

        if (isEmpty)
        {
            if (fieldDef.Mandatory)
                yield return $"Line {lineNumber}: Mandatory element '{fieldDef.Id}' ({fieldDef.Name}) " +
                             $"is missing in segment '{segTag}'.";
            yield break;
        }

        // ── Max length ───────────────────────────────────────────────────────
        if (fieldDef.MaxLength > 0 && value.Length > fieldDef.MaxLength)
            yield return $"Line {lineNumber}: Element '{fieldDef.Id}' ({fieldDef.Name}) in segment " +
                         $"'{segTag}' has value '{Truncate(value, 30)}' ({value.Length} chars) " +
                         $"which exceeds the maximum length of {fieldDef.MaxLength}.";

        // ── Data type ────────────────────────────────────────────────────────
        switch (fieldDef.DataType)
        {
            case "n":
                if (!IsNumeric(value))
                    yield return $"Line {lineNumber}: Element '{fieldDef.Id}' ({fieldDef.Name}) in segment " +
                                 $"'{segTag}' must be numeric but has value '{Truncate(value, 30)}'.";
                break;

            case "a":
                if (!IsAlphabetic(value))
                    yield return $"Line {lineNumber}: Element '{fieldDef.Id}' ({fieldDef.Name}) in segment " +
                                 $"'{segTag}' must be alphabetic but has value '{Truncate(value, 30)}'.";
                break;

                // "an" (alphanumeric): any printable character — no content check needed.
        }

        // ── Code list ────────────────────────────────────────────────────────
        if (fieldDef.IsLink)
        {
            var allowed = EdifactDefinitionService.LookupCodeList(directory, fieldDef.Id);
            if (allowed.Count > 0 && !allowed.ContainsKey(value))
                yield return $"Line {lineNumber}: Element '{fieldDef.Id}' ({fieldDef.Name}) in segment " +
                             $"'{segTag}' has unknown code value '{Truncate(value, 30)}'. " +
                             $"Expected one of the {allowed.Count} defined codes for this element." +
                             $" [CodeList:{directory}:{fieldDef.Id}:{segTag}:{fieldDef.Name}]";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [ThreadStatic]
    private static List<string>? _errors_buffer;

    /// <summary>
    /// Splits <paramref name="text"/> by the given <paramref name="separator"/>,
    /// honouring the EDIFACT release character <c>?</c> (which escapes the next
    /// character so it is not treated as a delimiter).
    /// </summary>
    private static List<string> SplitByUnescaped(string text, char separator)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool released = false;

        foreach (char c in text)
        {
            if (released)
            {
                current.Append(c);
                released = false;
            }
            else if (c == '?')
            {
                released = true;
                // Do not emit the release character itself; the next char is literal.
            }
            else if (c == separator)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        parts.Add(current.ToString());
        return parts;
    }

    private static bool IsNumeric(string s)
    {
        foreach (char c in s)
            if (c is not (>= '0' and <= '9') and not '.' and not '-')
                return false;
        return true;
    }

    private static bool IsAlphabetic(string s)
    {
        foreach (char c in s)
            if (!char.IsLetter(c))
                return false;
        return true;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
