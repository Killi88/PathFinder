using System.Text;
using System.Windows;

namespace PathFinder.Services;

/// <summary>
/// Generates syntax-highlighted HTML for pasting into Excel and places it on the clipboard.
/// Always uses Light Mode colors so content is readable on white Excel cells.
/// </summary>
internal static class ClipboardService
{
    // ──────────────────────────── XML light-mode colors ────────────────────────────
    private const string XmlBracket = "#0000FF"; // < > / ?
    private const string XmlTagName = "#A31515"; // element/PI names
    private const string XmlAttrName = "#FF0000"; // attribute names
    private const string XmlAttrEq = "#0000FF"; // =
    private const string XmlAttrQuote = "#000000"; // " "
    private const string XmlAttrValue = "#0000FF"; // attribute value text
    private const string XmlText = "#000000"; // text content
    private const string XmlComment = "#3A7A1E"; // <!-- -->

    // ──────────────────────────── JSON light-mode colors ────────────────────────────
    private const string JsonPunct = "#000000"; // { } [ ] , :
    private const string JsonKey = "#2E75B6"; // "key"
    private const string JsonString = "#A31515"; // "string value"
    private const string JsonPrimitive = "#098658"; // numbers, true, false, null

    // ──────────────────────────── YAML light-mode colors ────────────────────────────
    private const string YamlKeyColor = "#2E75B6"; // mapping keys
    private const string YamlStringColor = "#A31515"; // quoted string values
    private const string YamlCommentColor = "#3A7A1E"; // # comments
    private const string YamlPunctColor = "#000000"; // - : [ ] { }
    private const string YamlValueColor = "#098658"; // unquoted scalars (numbers, booleans, null)
    private const string YamlAnchorColor = "#6B5C00"; // & * anchors/aliases
    private const string YamlDocMarkerColor = "#0D62B5"; // --- ...

    // ──────────────────────────── public API ────────────────────────────

    internal static void CopyXmlAsHtml(string xmlText)
    {
        var colorized = ProcessLines(xmlText, ColorizeXmlLine);
        var html = BuildTableHtml(colorized, "Consolas, 'Courier New', monospace");
        SetClipboardHtml(html, xmlText);
    }

    internal static void CopyJsonAsHtml(string jsonText)
    {
        var html = BuildJsonHtml(jsonText);
        SetClipboardHtml(html, jsonText);
    }

    /// <summary>Returns the syntax-highlighted HTML table for the given JSON text (without touching the clipboard).</summary>
    internal static string BuildJsonHtml(string jsonText)
    {
        var colorized = ProcessLines(jsonText, ColorizeJsonLine);
        return BuildTableHtml(colorized, "Cascadia Mono, 'Segoe UI Mono', 'Courier New', monospace");
    }

    internal static void CopyYamlAsHtml(string yamlText)
    {
        var html = BuildYamlHtml(yamlText);
        SetClipboardHtml(html, yamlText);
    }

    /// <summary>Returns the syntax-highlighted HTML table for the given YAML text (without touching the clipboard).</summary>
    internal static string BuildYamlHtml(string yamlText)
    {
        var colorized = ProcessLines(yamlText, ColorizeYamlLine);
        return BuildTableHtml(colorized, "Cascadia Mono, 'Segoe UI Mono', 'Courier New', monospace");
    }

    // ──────────────────────────── line processing ────────────────────────────

    /// <summary>
    /// Splits the text into lines and applies the colorizer to each,
    /// converting leading spaces to &amp;nbsp; sequences for Excel indentation.
    /// </summary>
    private static List<string> ProcessLines(string text, Func<string, string> colorizer)
    {
        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            int leading = 0;
            while (leading < line.Length && line[leading] == ' ')
                leading++;

            string indent = leading > 0 ? string.Concat(Enumerable.Repeat("&nbsp;", leading)) : "";
            string colorized = leading < line.Length ? colorizer(line[leading..]) : "";
            result.Add(indent + colorized);
        }

        return result;
    }

    // ──────────────────────────── XML colorizer ────────────────────────────

    private static string ColorizeXmlLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";

        var sb = new StringBuilder();
        int pos = 0;

        while (pos < line.Length)
        {
            if (line[pos] == '<')
            {
                if (line.AsSpan(pos).StartsWith("<!--"))
                {
                    // XML comment
                    int end = line.IndexOf("-->", pos + 4, StringComparison.Ordinal);
                    int commentEnd = end >= 0 ? end + 3 : line.Length;
                    sb.Append(Span(XmlComment, EscapeHtml(line[pos..commentEnd])));
                    pos = commentEnd;
                }
                else if (line.AsSpan(pos).StartsWith("<![CDATA["))
                {
                    // CDATA section — show as plain text (no colorization)
                    int end = line.IndexOf("]]>", pos + 9, StringComparison.Ordinal);
                    int cdataEnd = end >= 0 ? end + 3 : line.Length;
                    sb.Append(EscapeHtml(line[pos..cdataEnd]));
                    pos = cdataEnd;
                }
                else
                {
                    // Regular tag or processing instruction
                    int tagEnd = FindTagEnd(line, pos);
                    sb.Append(ColorizeXmlTag(line, pos, tagEnd));
                    pos = tagEnd;
                }
            }
            else
            {
                // Text content between tags
                int nextTag = line.IndexOf('<', pos);
                if (nextTag < 0) nextTag = line.Length;
                sb.Append(Span(XmlText, EscapeHtml(line[pos..nextTag])));
                pos = nextTag;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Finds the index after the closing &gt; of a tag starting at <paramref name="start"/>,
    /// respecting quoted attribute values.
    /// </summary>
    private static int FindTagEnd(string text, int start)
    {
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = start + 1; i < text.Length; i++)
        {
            if (inQuote)
            {
                if (text[i] == quoteChar) inQuote = false;
            }
            else
            {
                if (text[i] == '"' || text[i] == '\'') { inQuote = true; quoteChar = text[i]; }
                else if (text[i] == '>') return i + 1;
            }
        }
        return text.Length;
    }

    /// <summary>
    /// Colorizes a single XML tag (from &lt; to &gt; inclusive) one character at a time.
    /// </summary>
    private static string ColorizeXmlTag(string text, int start, int end)
    {
        var sb = new StringBuilder();
        int pos = start;
        bool tagNameFound = false;

        while (pos < end)
        {
            char c = text[pos];

            if (c == '<')
            {
                sb.Append(Span(XmlBracket, "&lt;"));
                pos++;
            }
            else if (c == '>')
            {
                sb.Append(Span(XmlBracket, "&gt;"));
                pos++;
            }
            else if (!tagNameFound && (c == '/' || c == '?'))
            {
                // Leading / in </tag> or ? in <?pi  ?>
                sb.Append(Span(XmlBracket, EscapeHtml(c.ToString())));
                pos++;
            }
            else if (!tagNameFound && c != ' ' && c != '\t' && c != '\r' && c != '\n')
            {
                // Read tag / PI name
                int nameEnd = pos;
                while (nameEnd < end && text[nameEnd] != ' ' && text[nameEnd] != '>'
                       && text[nameEnd] != '/' && text[nameEnd] != '?' && text[nameEnd] != '\t')
                    nameEnd++;
                sb.Append(Span(XmlTagName, EscapeHtml(text[pos..nameEnd])));
                pos = nameEnd;
                tagNameFound = true;
            }
            else if (tagNameFound && (c == ' ' || c == '\t' || c == '\r' || c == '\n'))
            {
                // Whitespace between attributes — keep as-is
                sb.Append(EscapeHtml(c.ToString()));
                pos++;
            }
            else if (tagNameFound && (c == '/' || c == '?'))
            {
                // Self-closing / or closing ? of PI
                sb.Append(Span(XmlBracket, EscapeHtml(c.ToString())));
                pos++;
            }
            else if (tagNameFound)
            {
                // Attribute name
                int nameEnd = pos;
                while (nameEnd < end && text[nameEnd] != '=' && text[nameEnd] != ' '
                       && text[nameEnd] != '>' && text[nameEnd] != '/' && text[nameEnd] != '\t')
                    nameEnd++;
                sb.Append(Span(XmlAttrName, EscapeHtml(text[pos..nameEnd])));
                pos = nameEnd;

                if (pos < end && text[pos] == '=')
                {
                    sb.Append(Span(XmlAttrEq, "="));
                    pos++;

                    if (pos < end && (text[pos] == '"' || text[pos] == '\''))
                    {
                        char q = text[pos];
                        string quotHtml = q == '"' ? "&quot;" : "'";
                        sb.Append(Span(XmlAttrQuote, quotHtml));
                        pos++;

                        int valueEnd = pos;
                        while (valueEnd < end && text[valueEnd] != q)
                            valueEnd++;

                        sb.Append(Span(XmlAttrValue, EscapeHtml(text[pos..valueEnd])));
                        pos = valueEnd;

                        if (pos < end)
                        {
                            sb.Append(Span(XmlAttrQuote, quotHtml));
                            pos++;
                        }
                    }
                }
            }
            else
            {
                sb.Append(EscapeHtml(c.ToString()));
                pos++;
            }
        }

        return sb.ToString();
    }

    // ──────────────────────────── JSON colorizer ────────────────────────────

    /// <summary>
    /// Hand-written JSON line colorizer (matches the XML colorizer approach — no regex).
    /// Reads tokens character by character, distinguishes keys from string values by
    /// looking ahead for a ':' after a quoted string.
    /// </summary>
    private static string ColorizeJsonLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";

        var sb = new StringBuilder();
        int pos = 0;

        while (pos < line.Length)
        {
            char c = line[pos];

            if (c == '"')
            {
                // Read quoted string (handles escaped characters)
                int stringEnd = ReadJsonString(line, pos);
                string token = line[pos..stringEnd];

                // Look ahead past whitespace for ':' → key; otherwise → string value
                int peek = stringEnd;
                while (peek < line.Length && (line[peek] == ' ' || line[peek] == '\t'))
                    peek++;

                if (peek < line.Length && line[peek] == ':')
                    sb.Append(Span(JsonKey, EscapeHtml(token)));
                else
                    sb.Append(Span(JsonString, EscapeHtml(token)));

                pos = stringEnd;
            }
            else if (c == '-' || (c >= '0' && c <= '9'))
            {
                // Number literal
                int numEnd = pos + 1;
                while (numEnd < line.Length && "0123456789.eE+-".Contains(line[numEnd]))
                    numEnd++;
                sb.Append(Span(JsonPrimitive, EscapeHtml(line[pos..numEnd])));
                pos = numEnd;
            }
            else if (line.AsSpan(pos).StartsWith("true"))
            {
                sb.Append(Span(JsonPrimitive, "true"));
                pos += 4;
            }
            else if (line.AsSpan(pos).StartsWith("false"))
            {
                sb.Append(Span(JsonPrimitive, "false"));
                pos += 5;
            }
            else if (line.AsSpan(pos).StartsWith("null"))
            {
                sb.Append(Span(JsonPrimitive, "null"));
                pos += 4;
            }
            else if ("{}[],:.".Contains(c))
            {
                sb.Append(Span(JsonPunct, EscapeHtml(c.ToString())));
                pos++;
            }
            else
            {
                // Whitespace or other characters — pass through uncolored
                sb.Append(EscapeHtml(c.ToString()));
                pos++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns the index just past the closing quote of a JSON string starting at <paramref name="start"/>.
    /// Handles backslash escape sequences (including \").
    /// </summary>
    private static int ReadJsonString(string text, int start)
    {
        // start is at the opening '"'
        int i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2; // skip escaped character
            }
            else if (text[i] == '"')
            {
                return i + 1; // past the closing quote
            }
            else
            {
                i++;
            }
        }
        return text.Length; // unterminated string — consume rest of line
    }

    // ──────────────────────────── YAML colorizer ────────────────────────────

    private static string ColorizeYamlLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";

        var trimmed = line.TrimStart();

        // Document markers
        if (trimmed is "---" or "...")
        {
            int leading = line.Length - trimmed.Length;
            return (leading > 0 ? EscapeHtml(line[..leading]) : "")
                   + Span(YamlDocMarkerColor, EscapeHtml(trimmed));
        }

        // Comment line
        if (trimmed.StartsWith('#'))
        {
            int leading = line.Length - trimmed.Length;
            return (leading > 0 ? EscapeHtml(line[..leading]) : "")
                   + Span(YamlCommentColor, EscapeHtml(trimmed));
        }

        var sb = new StringBuilder();
        int pos = 0;

        // Emit leading whitespace
        while (pos < line.Length && (line[pos] == ' ' || line[pos] == '\t'))
        {
            sb.Append(EscapeHtml(line[pos].ToString()));
            pos++;
        }

        // Sequence indicator
        if (pos < line.Length && line[pos] == '-' && (pos + 1 >= line.Length || line[pos + 1] == ' '))
        {
            sb.Append(Span(YamlPunctColor, "-"));
            pos++;
            if (pos < line.Length && line[pos] == ' ')
            {
                sb.Append(" ");
                pos++;
            }
        }

        // Check for key: value pattern
        string rest = line[pos..];
        int colonIdx = rest.IndexOf(':');
        if (colonIdx > 0 && (colonIdx + 1 >= rest.Length || rest[colonIdx + 1] == ' '))
        {
            // Key
            string key = rest[..colonIdx];
            sb.Append(Span(YamlKeyColor, EscapeHtml(key)));
            sb.Append(Span(YamlPunctColor, ":"));
            pos += colonIdx + 1;

            if (pos < line.Length && line[pos] == ' ')
            {
                sb.Append(" ");
                pos++;
            }

            // Remaining value part
            if (pos < line.Length)
            {
                string value = line[pos..];
                // Check for inline comment
                int commentIdx = FindYamlInlineComment(value);
                string valuePart = commentIdx >= 0 ? value[..commentIdx].TrimEnd() : value;
                string? commentPart = commentIdx >= 0 ? value[commentIdx..] : null;

                sb.Append(ColorizeYamlValue(valuePart));
                if (commentPart is not null)
                {
                    // space before comment
                    if (commentIdx > 0 && commentIdx < value.Length)
                    {
                        string gap = value[valuePart.Length..commentIdx];
                        if (gap.Length > 0) sb.Append(EscapeHtml(gap));
                    }
                    sb.Append(Span(YamlCommentColor, EscapeHtml(commentPart)));
                }
            }
        }
        else
        {
            // No key — just a value or continuation
            if (pos < line.Length)
            {
                string value = line[pos..];
                int commentIdx = FindYamlInlineComment(value);
                string valuePart = commentIdx >= 0 ? value[..commentIdx].TrimEnd() : value;
                string? commentPart = commentIdx >= 0 ? value[commentIdx..] : null;

                sb.Append(ColorizeYamlValue(valuePart));
                if (commentPart is not null)
                {
                    string gap = value[valuePart.Length..commentIdx];
                    if (gap.Length > 0) sb.Append(EscapeHtml(gap));
                    sb.Append(Span(YamlCommentColor, EscapeHtml(commentPart)));
                }
            }
        }

        return sb.ToString();
    }

    private static string ColorizeYamlValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        // Anchor or alias
        if (value.StartsWith('&') || value.StartsWith('*'))
            return Span(YamlAnchorColor, EscapeHtml(value));

        // Quoted strings
        if ((value.StartsWith('"') && value.EndsWith('"'))
            || (value.StartsWith('\'') && value.EndsWith('\'')))
            return Span(YamlStringColor, EscapeHtml(value));

        // Block scalar indicators
        if (value is "|" or ">" or "|-" or ">-" or "|+" or ">+")
            return Span(YamlPunctColor, EscapeHtml(value));

        // Booleans, null
        if (value is "true" or "false" or "null" or "~"
            or "True" or "False" or "Null"
            or "TRUE" or "FALSE" or "NULL")
            return Span(YamlValueColor, EscapeHtml(value));

        // Numbers
        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return Span(YamlValueColor, EscapeHtml(value));

        // Plain scalar (unquoted string)
        return Span(YamlStringColor, EscapeHtml(value));
    }

    private static int FindYamlInlineComment(string text)
    {
        // A YAML inline comment starts with " #" (space then hash) outside quotes
        bool inSingle = false, inDouble = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (c == '#' && !inSingle && !inDouble && i > 0 && text[i - 1] == ' ')
                return i;
        }
        return -1;
    }

    // ──────────────────────────── HTML building ────────────────────────────

    private static string BuildTableHtml(List<string> colorizedLines, string fontFamily)
    {
        var sb = new StringBuilder();
        sb.Append($"<table style=\"font-family:{fontFamily};font-size:9.5pt;border-collapse:collapse;\">");
        sb.Append("<tbody>");

        foreach (var line in colorizedLines)
            sb.Append($"<tr><td style=\"white-space:pre;\">{line}</td></tr>");

        sb.Append("</tbody>");
        sb.Append("</table>");
        return sb.ToString();
    }

    // ──────────────────────────── clipboard helpers ────────────────────────────

    private static void SetClipboardHtml(string htmlFragment, string plainText)
    {
        const string htmlPre = "<html>\r\n<body>\r\n<!--StartFragment-->";
        const string htmlPost = "<!--EndFragment-->\r\n</body>\r\n</html>";

        // CF_HTML header format — all byte offsets padded to 10 digits so header size is fixed
        const string headerTemplate =
            "Version:0.9\r\n" +
            "StartHTML:{0:D10}\r\n" +
            "EndHTML:{1:D10}\r\n" +
            "StartFragment:{2:D10}\r\n" +
            "EndFragment:{3:D10}\r\n";

        // Compute fixed header size using dummy values (all D10-padded → same byte width)
        string dummyHeader = string.Format(headerTemplate, 0, 0, 0, 0);
        int headerLen = Encoding.UTF8.GetByteCount(dummyHeader);

        int startHtml = headerLen;
        int startFragment = headerLen + Encoding.UTF8.GetByteCount(htmlPre);
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(htmlFragment);
        int endHtml = endFragment + Encoding.UTF8.GetByteCount(htmlPost);

        string header = string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment);
        string cfHtml = header + htmlPre + htmlFragment + htmlPost;

        var dataObj = new DataObject();
        dataObj.SetData(DataFormats.Html, cfHtml);
        dataObj.SetData(DataFormats.Text, plainText);
        Clipboard.SetDataObject(dataObj, true);
    }

    // ──────────────────────────── utilities ────────────────────────────

    private static string Span(string color, string content) =>
        $"<span style=\"color:{color}\">{content}</span>";

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
