using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PathFinder.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PathFinder.Services;

public static class YamlService
{
    /// <summary>
    /// Pretty-prints the given YAML string with 2-space indentation.
    /// Throws <see cref="YamlDotNet.Core.YamlException"/> on invalid YAML.
    /// </summary>
    public static string FormatYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return string.Empty;

        // Replace leading tabs with 2 spaces — tabs are invalid YAML indentation
        yaml = Regex.Replace(yaml, @"(?m)^(\t+)", m => new string(' ', m.Value.Length * 2));

        var deserializer = new DeserializerBuilder().Build();
        var serializer = new SerializerBuilder()
            .WithIndentedSequences()
            .Build();

        using var reader = new StringReader(yaml);
        var docs = new List<object?>();
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();
        while (parser.Accept<DocumentStart>(out _))
        {
            var doc = deserializer.Deserialize(parser);
            docs.Add(doc);
        }

        var sb = new StringBuilder();
        for (int i = 0; i < docs.Count; i++)
        {
            if (i > 0 || docs.Count > 1)
                sb.AppendLine("---");
            sb.Append(serializer.Serialize(docs[i]));
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Returns the YAML path (e.g. $.store.books[0].title) of the node
    /// whose line is on or immediately before <paramref name="targetLine"/>.
    /// Returns null if the YAML is invalid or no node is found.
    /// </summary>
    public static string? GetYamlPathAtLine(string yamlText, int targetLine)
    {
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yamlText);
            stream.Load(reader);

            string? bestPath = null;
            int bestLine = 0;

            foreach (var doc in stream.Documents)
                FindNodeAtLine(doc.RootNode, "$", targetLine, ref bestPath, ref bestLine, isRoot: true);

            return bestPath;
        }
        catch { return null; }
    }

    /// <summary>
    /// Evaluates a dot-notation path expression (e.g. $.store.books[0].title)
    /// against the YAML document and returns matching nodes.
    /// </summary>
    public static List<XPathResultItem> ExecuteYamlPath(string yamlText, string expression)
    {
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(yamlText);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"Invalid YAML: {ex.Message}", ex);
        }

        if (stream.Documents.Count == 0)
            return [];

        var root = stream.Documents[0].RootNode;
        var segments = ParsePathSegments(expression);
        if (segments is null)
            throw new InvalidOperationException($"Invalid YAMLPath expression: {expression}");

        var results = new List<XPathResultItem>();
        WalkPath(root, segments, 0, "$", results);
        return results;
    }

    /// <summary>
    /// Enumerates all nodes in the YAML document with their paths,
    /// value previews, and line numbers.
    /// </summary>
    public static List<XPathResultItem> GetAllPaths(string yamlText)
    {
        var results = new List<XPathResultItem>();
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yamlText);
            stream.Load(reader);

            foreach (var doc in stream.Documents)
                CollectAllPaths(doc.RootNode, "$", results);
        }
        catch { }
        return results;
    }

    /// <summary>
    /// Converts YAML to JSON using 4-space indentation.
    /// </summary>
    public static string ConvertYamlToJson(string yaml)
    {
        var deserializer = new DeserializerBuilder().Build();
        using var reader = new StringReader(yaml);
        var obj = deserializer.Deserialize(reader);
        return JsonConvert.SerializeObject(ConvertToJsonFriendly(obj), Formatting.Indented);
    }

    /// <summary>
    /// Converts JSON to YAML with 2-space indentation.
    /// </summary>
    public static string ConvertJsonToYaml(string json)
    {
        var token = JToken.Parse(json);
        var obj = ConvertJTokenToObject(token);
        var serializer = new SerializerBuilder()
            .WithIndentedSequences()
            .Build();
        return serializer.Serialize(obj!).TrimEnd('\r', '\n');
    }

    // ──────────────────────────── private helpers ────────────────────────────

    private static void FindNodeAtLine(YamlNode node, string currentPath, int targetLine,
        ref string? bestPath, ref int bestLine, bool isRoot = false)
    {
        int line = (int)node.Start.Line; // 1-based

        switch (node)
        {
            case YamlMappingNode mapping:
                // Only record the root mapping as a match (for $)
                if (isRoot && line <= targetLine && line > bestLine)
                {
                    bestPath = currentPath;
                    bestLine = line;
                }
                foreach (var entry in mapping.Children)
                {
                    string key = ((YamlScalarNode)entry.Key).Value ?? "";
                    string childPath = $"{currentPath}.{key}";
                    int keyLine = (int)entry.Key.Start.Line;
                    if (keyLine <= targetLine && keyLine > bestLine)
                    {
                        bestPath = childPath;
                        bestLine = keyLine;
                    }
                    FindNodeAtLine(entry.Value, childPath, targetLine, ref bestPath, ref bestLine);
                }
                break;
            case YamlSequenceNode sequence:
                if (line <= targetLine && line > bestLine)
                {
                    bestPath = currentPath;
                    bestLine = line;
                }
                for (int i = 0; i < sequence.Children.Count; i++)
                    FindNodeAtLine(sequence.Children[i], $"{currentPath}[{i}]", targetLine, ref bestPath, ref bestLine);
                break;
            default:
                if (line <= targetLine && line > bestLine)
                {
                    bestPath = currentPath;
                    bestLine = line;
                }
                break;
        }
    }

    private static void CollectAllPaths(YamlNode node, string currentPath, List<XPathResultItem> results)
    {
        int line = (int)node.Start.Line;
        string preview = node switch
        {
            YamlMappingNode m => $"{{{m.Children.Count}}}",
            YamlSequenceNode s => $"[{s.Children.Count}]",
            YamlScalarNode sc => FormatValuePreview(sc.Value ?? ""),
            _ => ""
        };

        results.Add(new XPathResultItem
        {
            XPath = currentPath,
            Preview = preview,
            LineNumber = line
        });

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    string key = ((YamlScalarNode)entry.Key).Value ?? "";
                    CollectAllPaths(entry.Value, $"{currentPath}.{key}", results);
                }
                break;
            case YamlSequenceNode sequence:
                for (int i = 0; i < sequence.Children.Count; i++)
                    CollectAllPaths(sequence.Children[i], $"{currentPath}[{i}]", results);
                break;
        }
    }

    private static string FormatValuePreview(string value) =>
        value.Length > 80 ? value[..80] + "\u2026" : value;

    /// <summary>
    /// Converts YamlDotNet-deserialized objects (Dictionary&lt;object,object&gt;, List&lt;object&gt;, string)
    /// into JSON-friendly types (Dictionary&lt;string,object&gt;, List&lt;object&gt;, primitives).
    /// </summary>
    private static object? ConvertToJsonFriendly(object? obj)
    {
        return obj switch
        {
            Dictionary<object, object> dict =>
                dict.ToDictionary(kv => kv.Key.ToString()!, kv => ConvertToJsonFriendly(kv.Value)),
            List<object> list =>
                list.Select(ConvertToJsonFriendly).ToList(),
            string s => s,
            _ => obj
        };
    }

    /// <summary>
    /// Converts a JToken tree into plain .NET objects suitable for YamlDotNet serialization.
    /// </summary>
    private static object? ConvertJTokenToObject(JToken token)
    {
        return token switch
        {
            JObject obj => obj.Properties().ToDictionary(p => p.Name, p => ConvertJTokenToObject(p.Value)),
            JArray arr => arr.Select(ConvertJTokenToObject).ToList(),
            JValue val => val.Value,
            _ => token.ToString()
        };
    }

    /// <summary>
    /// Parses a $-prefixed dot-notation path into segments.
    /// e.g. "$.store.books[0].title" → ["store", "books", "[0]", "title"]
    /// Returns null if the expression doesn't start with $.
    /// </summary>
    private static List<string>? ParsePathSegments(string expression)
    {
        expression = expression.Trim();
        if (!expression.StartsWith('$'))
            return null;

        if (expression == "$")
            return [];

        var rest = expression[1..];
        if (rest.StartsWith('.'))
            rest = rest[1..];

        var segments = new List<string>();
        var matches = Regex.Matches(rest, @"([^\.\[\]]+)|\[(\d+)\]");
        foreach (Match m in matches)
        {
            if (m.Groups[2].Success)
                segments.Add($"[{m.Groups[2].Value}]");
            else
                segments.Add(m.Groups[1].Value);
        }
        return segments;
    }

    private static void WalkPath(YamlNode node, List<string> segments, int segIndex,
        string currentPath, List<XPathResultItem> results)
    {
        if (segIndex >= segments.Count)
        {
            int line = (int)node.Start.Line;
            string preview = node switch
            {
                YamlMappingNode m => "{…}",
                YamlSequenceNode s => "[…]",
                YamlScalarNode sc => FormatValuePreview(sc.Value ?? ""),
                _ => ""
            };
            if (preview.Length > 80) preview = preview[..80] + "…";

            results.Add(new XPathResultItem
            {
                XPath = currentPath,
                Preview = preview,
                LineNumber = line
            });
            return;
        }

        var segment = segments[segIndex];

        if (segment.StartsWith('[') && segment.EndsWith(']'))
        {
            // Array index
            if (node is YamlSequenceNode seq && int.TryParse(segment[1..^1], out int idx) && idx < seq.Children.Count)
                WalkPath(seq.Children[idx], segments, segIndex + 1, $"{currentPath}[{idx}]", results);
        }
        else if (segment == "*")
        {
            // Wildcard — match all children
            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var entry in mapping.Children)
                    {
                        string key = ((YamlScalarNode)entry.Key).Value ?? "";
                        WalkPath(entry.Value, segments, segIndex + 1, $"{currentPath}.{key}", results);
                    }
                    break;
                case YamlSequenceNode sequence:
                    for (int i = 0; i < sequence.Children.Count; i++)
                        WalkPath(sequence.Children[i], segments, segIndex + 1, $"{currentPath}[{i}]", results);
                    break;
            }
        }
        else
        {
            // Named key
            if (node is YamlMappingNode mapping)
            {
                foreach (var entry in mapping.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value == segment)
                    {
                        WalkPath(entry.Value, segments, segIndex + 1, $"{currentPath}.{segment}", results);
                        break;
                    }
                }
            }
        }
    }
}
