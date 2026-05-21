using System.IO;
using System.Text.Json;

namespace PathFinder.Models;

internal sealed class SyntaxColorSettings
{
    public Dictionary<string, string> DarkColors { get; set; } = new();
    public Dictionary<string, string> LightColors { get; set; } = new();

    public static readonly string[] XmlKeys =
    [
        "XmlComment", "XmlCData", "XmlDocType", "XmlTagName",
        "XmlAttrName", "XmlAttrValue", "XmlEntity", "XmlBracket", "XmlText"
    ];

    public static readonly string[] JsonKeys =
    [
        "JsonKey", "JsonString", "JsonNumber", "JsonKeyword"
    ];

    public static readonly string[] EdiKeys =
    [
        "EdiSegTag", "EdiSegTerm", "EdiElemSep", "EdiCompSep", "EdiRelease", "EdiData"
    ];

    public static readonly string[] YamlKeys =
    [
        "YamlKey", "YamlValue", "YamlComment", "YamlAnchor", "YamlTag", "YamlDocMarker"
    ];

    public static readonly string[] AllKeys = [.. XmlKeys, .. JsonKeys, .. EdiKeys, .. YamlKeys];

    public static readonly Dictionary<string, string> DefaultDarkColors = new()
    {
        ["XmlComment"] = "#6A9955",
        ["XmlCData"] = "#CE9178",
        ["XmlDocType"] = "#808080",
        ["XmlTagName"] = "#4EC9B0",
        ["XmlAttrName"] = "#9CDCFE",
        ["XmlAttrValue"] = "#F0A070",
        ["XmlEntity"] = "#DCDCAA",
        ["XmlBracket"] = "#808080",
        ["JsonKey"] = "#9CDCFE",
        ["JsonString"] = "#F0A070",
        ["JsonNumber"] = "#B5CEA8",
        ["JsonKeyword"] = "#DCDCAA",
        ["EdiSegTag"] = "#4EC9B0",
        ["EdiSegTerm"] = "#808080",
        ["EdiElemSep"] = "#808080",
        ["EdiCompSep"] = "#9CDCFE",
        ["EdiRelease"] = "#DCDCAA",
        ["XmlText"] = "#D4D4D4",
        ["EdiData"] = "#D4D4D4",
        ["YamlKey"] = "#9CDCFE",
        ["YamlValue"] = "#CE9178",
        ["YamlComment"] = "#6A9955",
        ["YamlAnchor"] = "#DCDCAA",
        ["YamlTag"] = "#4EC9B0",
        ["YamlDocMarker"] = "#569CD6"
    };

    public static readonly Dictionary<string, string> DefaultLightColors = new()
    {
        ["XmlComment"] = "#3A7A1E",
        ["XmlCData"] = "#A3420C",
        ["XmlDocType"] = "#6A6A6A",
        ["XmlTagName"] = "#1A7070",
        ["XmlAttrName"] = "#0D62B5",
        ["XmlAttrValue"] = "#A84300",
        ["XmlEntity"] = "#7A6800",
        ["XmlBracket"] = "#6A6A6A",
        ["JsonKey"] = "#0D62B5",
        ["JsonString"] = "#A84300",
        ["JsonNumber"] = "#267326",
        ["JsonKeyword"] = "#6B5C00",
        ["EdiSegTag"] = "#1A7070",
        ["EdiSegTerm"] = "#6A6A6A",
        ["EdiElemSep"] = "#6A6A6A",
        ["EdiCompSep"] = "#0D62B5",
        ["EdiRelease"] = "#7A6800",
        ["XmlText"] = "#1E1E1E",
        ["EdiData"] = "#1E1E1E",
        ["YamlKey"] = "#0D62B5",
        ["YamlValue"] = "#A84300",
        ["YamlComment"] = "#3A7A1E",
        ["YamlAnchor"] = "#6B5C00",
        ["YamlTag"] = "#1A7070",
        ["YamlDocMarker"] = "#0D62B5"
    };

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["XmlComment"] = "Comment",
        ["XmlCData"] = "CDATA",
        ["XmlDocType"] = "DocType / Declaration",
        ["XmlTagName"] = "Tag Name",
        ["XmlAttrName"] = "Attribute Name",
        ["XmlAttrValue"] = "Attribute Value",
        ["XmlEntity"] = "Entity Reference",
        ["XmlBracket"] = "Brackets / Punctuation",
        ["JsonKey"] = "Key",
        ["JsonString"] = "String Value",
        ["JsonNumber"] = "Number",
        ["JsonKeyword"] = "Keyword (true/false/null)",
        ["EdiSegTag"] = "Segment Tag",
        ["EdiSegTerm"] = "Segment Terminator (')",
        ["EdiElemSep"] = "Element Separator (+)",
        ["EdiCompSep"] = "Component Separator (:)",
        ["EdiRelease"] = "Release Character (?)",
        ["XmlText"] = "Text Content",
        ["EdiData"] = "Data Values",
        ["YamlKey"] = "Key",
        ["YamlValue"] = "String Value",
        ["YamlComment"] = "Comment",
        ["YamlAnchor"] = "Anchor / Alias",
        ["YamlTag"] = "Tag",
        ["YamlDocMarker"] = "Document Marker"
    };

    public string GetColor(string key, bool dark)
    {
        var dict = dark ? DarkColors : LightColors;
        var defaults = dark ? DefaultDarkColors : DefaultLightColors;
        return dict.TryGetValue(key, out var color) && IsValidHex(color) ? color : defaults[key];
    }

    public static bool IsValidHex(string? hex)
        => hex is { Length: 7 } && hex[0] == '#'
           && hex.AsSpan(1).IndexOfAnyExcept("0123456789ABCDEFabcdef") < 0;

    public static SyntaxColorSettings CreateDefault() => new()
    {
        DarkColors = new Dictionary<string, string>(DefaultDarkColors),
        LightColors = new Dictionary<string, string>(DefaultLightColors)
    };

    internal static string ColorsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PathFinder",
        "colors.json");

    internal static SyntaxColorSettings Load(string? path = null)
    {
        try
        {
            path ??= ColorsFilePath;
            if (!File.Exists(path)) return CreateDefault();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SyntaxColorSettings>(json) ?? CreateDefault();
        }
        catch { return CreateDefault(); }
    }

    internal static void Save(SyntaxColorSettings settings, string? path = null)
    {
        try
        {
            path ??= ColorsFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
