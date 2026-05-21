using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathFinder.Models;

// ── EdifactStructureItem ──────────────────────────────────────────────────────
// Represents one entry in a message structure: either a plain segment or a
// nested segment group.  The "kind" discriminator drives which properties are
// populated:
//   kind == "segment" → Tag, Mandatory, MaxOccurrences
//   kind == "group"   → Name, Mandatory, MaxOccurrences, Items

public class EdifactStructureItem
{
    /// <summary>"segment" or "group"</summary>
    public string Kind { get; init; } = "segment";

    /// <summary>Segment tag, e.g. "BGM". Null for groups.</summary>
    public string? Tag { get; init; }

    /// <summary>Group name, e.g. "SG1". Null for segments.</summary>
    public string? Name { get; init; }

    public bool Mandatory { get; init; }

    public int MaxOccurrences { get; init; } = 1;

    /// <summary>Child items in a group. Null for segments.</summary>
    public List<EdifactStructureItem>? Items { get; init; }
}

// ── EdifactFieldDef ───────────────────────────────────────────────────────────
// One field (simple or composite) in a segment definition.

public class EdifactFieldDef
{
    /// <summary>Element ID, e.g. "1004" or composite ID "C002".</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public bool Mandatory { get; init; }

    /// <summary>True when this field is a composite (C/S prefix) with Components.</summary>
    public bool IsComposite { get; init; }

    /// <summary>"an" (alphanumeric), "n" (numeric), "a" (alphabetic).</summary>
    public string DataType { get; init; } = "an";

    /// <summary>Maximum character length; 0 means no limit defined.</summary>
    public int MaxLength { get; init; }

    /// <summary>
    /// True when this element ID links to a code list on edifactory.de —
    /// the validator uses the directory's CodeLists dictionary to validate values.
    /// </summary>
    public bool IsLink { get; init; }

    /// <summary>Component fields for composite elements. Null for simple fields.</summary>
    public List<EdifactFieldDef>? Components { get; init; }
}

// ── EdifactSegmentDef ─────────────────────────────────────────────────────────

public class EdifactSegmentDef
{
    public string Tag { get; init; } = "";
    public List<EdifactFieldDef> Fields { get; init; } = [];
}

// ── EdifactMessageDef ─────────────────────────────────────────────────────────

public class EdifactMessageDef
{
    /// <summary>Ordered list of top-level structure items (segments and groups).</summary>
    public List<EdifactStructureItem> Structure { get; init; } = [];
}

// ── EdifactDirectoryDef ───────────────────────────────────────────────────────

public class EdifactDirectoryDef
{
    /// <summary>Message definitions keyed by message type code (e.g. "IFTMCS").</summary>
    public Dictionary<string, EdifactMessageDef> Messages { get; init; } = [];

    /// <summary>Segment definitions keyed by segment tag (e.g. "BGM").</summary>
    public Dictionary<string, EdifactSegmentDef> Segments { get; init; } = [];

    /// <summary>
    /// Code list values keyed by data element ID (e.g. "1001").
    /// Each code list maps code value → description.
    /// Only elements whose IDs appear as hyperlinks in segment popups are included.
    /// </summary>
    [JsonConverter(typeof(CodeListsConverter))]
    public Dictionary<string, Dictionary<string, string>> CodeLists { get; init; } = [];
}

// ── EdifactAllDefinitions ─────────────────────────────────────────────────────

/// <summary>Root object that is deserialized from EdifactDefinitions.json(.gz).</summary>
public class EdifactAllDefinitions
{
    public string Version { get; init; } = "1.0";
    public string GeneratedAt { get; init; } = "";

    /// <summary>Directory definitions keyed by directory code (e.g. "D96A").</summary>
    public Dictionary<string, EdifactDirectoryDef> Directories { get; init; } = [];

    /// <summary>ISO9735 service segment definitions (UNA, UNB, UNG, UNE, UNH, UNT, UNZ).</summary>
    public Dictionary<string, EdifactSegmentDef> ServiceSegments { get; init; } = [];
}

// ── CodeListsConverter ────────────────────────────────────────────────────────
// Handles both the old format (code list as JSON array of strings) and the new
// format (code list as JSON object mapping code → description).

internal sealed class CodeListsConverter : JsonConverter<Dictionary<string, Dictionary<string, string>>>
{
    public override Dictionary<string, Dictionary<string, string>> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for CodeLists.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            var elementId = reader.GetString()!;
            reader.Read();

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // New format: { "code": "description", ... }
                var dict = new Dictionary<string, string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    var code = reader.GetString()!;
                    reader.Read();
                    var desc = reader.GetString() ?? "";
                    dict[code] = desc;
                }
                result[elementId] = dict;
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Old format: [ "code1", "code2", ... ]
                var dict = new Dictionary<string, string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    var code = reader.GetString()!;
                    dict[code] = "";
                }
                result[elementId] = dict;
            }
            else
            {
                throw new JsonException($"Unexpected token {reader.TokenType} for code list '{elementId}'.");
            }
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer, Dictionary<string, Dictionary<string, string>> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
