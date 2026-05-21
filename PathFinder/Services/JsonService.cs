using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PathFinder.Models;

namespace PathFinder.Services;

public static class JsonService
{
    /// <summary>
    /// Pretty-prints the given JSON string with 4-space indentation.
    /// </summary>
    public static string FormatJson(string json)
    {
        var token = JToken.Parse(json);
        // Serialize with Newtonsoft using 4-space indent
        var sb = new System.Text.StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw)
        {
            Formatting = Formatting.Indented,
            Indentation = 4,
            IndentChar = ' '
        };
        token.WriteTo(writer);
        return sb.ToString();
    }

    /// <summary>
    /// Compresses the given JSON string to a single line with no whitespace.
    /// </summary>
    public static string MinifyJson(string json)
    {
        var token = JToken.Parse(json);
        var sb = new System.Text.StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };
        token.WriteTo(writer);
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSONPath (e.g. $.store.books[0].title) of the token
    /// whose opening line is on or immediately before <paramref name="targetLine"/>.
    /// Returns null if the JSON is invalid or no token is found.
    /// </summary>
    public static string? GetJsonPathAtLine(string jsonText, int targetLine)
    {
        try
        {
            var loadSettings = new JsonLoadSettings { LineInfoHandling = LineInfoHandling.Load };
            using var reader = new JsonTextReader(new StringReader(jsonText));
            var root = JToken.Load(reader, loadSettings);

            JToken? best = null;
            int bestLine = 0;
            FindTokenAtLine(root, targetLine, ref best, ref bestLine);

            if (best is null) return null;

            // Produce a $-prefixed path.
            // path starting with '[' means an array index at root — no dot needed.
            var path = best.Path;
            if (string.IsNullOrEmpty(path)) return "$";
            return path.StartsWith('[') ? "$" + path : "$." + path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes a JSONPath expression and returns matching tokens as result items.
    /// </summary>
    public static List<XPathResultItem> ExecuteJsonPath(string jsonText, string expression)
    {
        var loadSettings = new JsonLoadSettings { LineInfoHandling = LineInfoHandling.Load };
        using var reader = new JsonTextReader(new StringReader(jsonText));
        var root = JToken.Load(reader, loadSettings);

        IEnumerable<JToken> matches;
        try
        {
            matches = root.SelectTokens(expression);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSONPath expression: {ex.Message}", ex);
        }

        var results = new List<XPathResultItem>();
        foreach (var token in matches)
        {
            int line = 0;
            if (token is IJsonLineInfo li && li.HasLineInfo())
                line = li.LineNumber;

            var path = token.Path;
            string jsonPath = string.IsNullOrEmpty(path) ? "$"
                            : path.StartsWith('[') ? "$" + path
                            : "$." + path;

            string preview = token.Type switch
            {
                JTokenType.Object => "{…}",
                JTokenType.Array => "[…]",
                _ => token.ToString()
            };
            if (preview.Length > 80) preview = preview[..80] + "…";

            results.Add(new XPathResultItem
            {
                XPath = jsonPath,
                Preview = preview,
                LineNumber = line
            });
        }
        return results;
    }

    /// <summary>
    /// Enumerates all tokens in the JSON document with their paths,
    /// value previews, and line numbers.
    /// </summary>
    public static List<XPathResultItem> GetAllPaths(string jsonText)
    {
        var results = new List<XPathResultItem>();
        JToken root;
        try
        {
            var loadSettings = new JsonLoadSettings { LineInfoHandling = LineInfoHandling.Load };
            using var reader = new JsonTextReader(new StringReader(jsonText));
            root = JToken.Load(reader, loadSettings);
        }
        catch { return results; }

        CollectAllJsonPaths(root, results);
        return results;
    }

    private static void CollectAllJsonPaths(JToken token, List<XPathResultItem> results)
    {
        if (token is JProperty prop)
        {
            CollectAllJsonPaths(prop.Value, results);
            return;
        }

        int line = token is IJsonLineInfo li && li.HasLineInfo() ? li.LineNumber : 0;
        var rawPath = token.Path;
        string jsonPath = string.IsNullOrEmpty(rawPath) ? "$"
                        : rawPath.StartsWith('[') ? "$" + rawPath
                        : "$." + rawPath;

        string preview = token.Type switch
        {
            JTokenType.Object => $"{{{((JObject)token).Count}}}",
            JTokenType.Array => $"[{((JArray)token).Count}]",
            _ => FormatValuePreview(token.ToString())
        };

        results.Add(new XPathResultItem
        {
            XPath = jsonPath,
            Preview = preview,
            LineNumber = line
        });

        foreach (var child in token.Children())
            CollectAllJsonPaths(child, results);
    }

    private static string FormatValuePreview(string value) =>
        value.Length > 80 ? value[..80] + "\u2026" : value;

    private static void FindTokenAtLine(JToken token, int targetLine,
        ref JToken? best, ref int bestLine)
    {
        if (token is IJsonLineInfo li && li.HasLineInfo())
        {
            int line = li.LineNumber;
            if (line <= targetLine && line > bestLine)
            {
                best = token;
                bestLine = line;
            }
        }

        foreach (var child in token.Children())
            FindTokenAtLine(child, targetLine, ref best, ref bestLine);
    }
}
