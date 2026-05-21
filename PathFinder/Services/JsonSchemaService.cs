using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PathFinder.Models;
using YamlDotNet.Serialization;

namespace PathFinder.Services;

/// <summary>
/// Parses JSON Schema and OpenAPI specifications into <see cref="SchemaNode"/> trees for visualization.
/// Supports JSON and YAML input formats, $ref resolution, allOf/oneOf/anyOf composition.
/// </summary>
public static class JsonSchemaService
{
    private const int MaxDepth = 50;

    // ──────────────────────────── Detection ────────────────────────────

    /// <summary>
    /// Returns true when the text looks like a JSON Schema, OpenAPI spec, or schema fragment.
    /// </summary>
    public static bool IsJsonSchemaContent(string text)
    {
        try
        {
            var obj = ParseToJObject(text);
            if (obj is null) return false;

            // Recognise by common schema keywords
            if (obj["$schema"] is not null) return true;
            if (obj["$defs"] is not null) return true;
            if (obj["definitions"] is not null) return true;
            if (obj["openapi"] is not null) return true;
            if (obj["swagger"] is not null) return true;
            if (obj["properties"] is not null) return true;
            if (obj["allOf"] is not null) return true;
            if (obj["anyOf"] is not null) return true;
            if (obj["oneOf"] is not null) return true;
            if (obj["type"]?.ToString() == "object") return true;

            // Schema fragment: every top-level value is an object that looks like a schema
            return IsSchemaFragment(obj);
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────── Parsing ────────────────────────────

    /// <summary>
    /// Parses JSON or YAML Schema text into a list of <see cref="SchemaNode"/> roots.
    /// </summary>
    public static List<SchemaNode> ParseJsonSchema(string text)
    {
        var obj = ParseToJObject(text)
            ?? throw new InvalidOperationException("Could not parse schema. Make sure it is valid JSON or YAML.");

        // Pass 1: collect all named definitions for $ref resolution
        var defs = new Dictionary<string, JObject>(StringComparer.Ordinal);
        CollectDefinitions(obj, defs);

        // Pass 2: build the visible tree
        var roots = new List<SchemaNode>();

        if (obj["openapi"] is not null || obj["swagger"] is not null)
        {
            // OpenAPI spec — show components.schemas as top-level nodes
            var schemas = obj.SelectToken("components.schemas") as JObject
                ?? obj.SelectToken("definitions") as JObject;
            if (schemas is not null)
            {
                foreach (var prop in schemas.Properties())
                {
                    if (prop.Value is JObject schemaObj)
                    {
                        var node = BuildNode(prop.Name, schemaObj, isRequired: false,
                            depth: 0, visited: [], defs: defs);
                        roots.Add(node);
                    }
                }
            }
        }
        else if (IsSchemaFragment(obj))
        {
            // Schema fragment: each top-level key is a named schema
            foreach (var prop in obj.Properties())
            {
                if (prop.Value is JObject schemaObj)
                {
                    var node = BuildNode(prop.Name, schemaObj, isRequired: false,
                        depth: 0, visited: [], defs: defs);
                    roots.Add(node);
                }
            }
        }
        else
        {
            // Standard JSON Schema
            string rootName = obj["title"]?.ToString() ?? "root";
            var root = BuildNode(rootName, obj, isRequired: true,
                depth: 0, visited: [], defs: defs);

            // If root produced no children but $defs exist, surface them
            if (root.Children.Count == 0)
            {
                var defsObj = obj["$defs"] as JObject ?? obj["definitions"] as JObject;
                if (defsObj is not null)
                {
                    foreach (var prop in defsObj.Properties())
                    {
                        if (prop.Value is JObject defSchema)
                        {
                            var child = BuildNode(prop.Name, defSchema, isRequired: false,
                                depth: 1, visited: [], defs: defs);
                            root.Children.Add(child);
                        }
                    }
                }
            }

            roots.Add(root);
        }

        if (roots.Count == 0)
            throw new InvalidOperationException("No schemas found in the document.");

        return roots;
    }

    // ──────────────────────────── Sample JSON Generation ────────────────────────────

    /// <summary>
    /// Generates a sample JSON string from a JSON Schema or OpenAPI spec.
    /// </summary>
    public static string GenerateSampleJson(string text)
    {
        var obj = ParseToJObject(text)
            ?? throw new InvalidOperationException("Failed to parse schema.");

        var defs = new Dictionary<string, JObject>();
        CollectDefinitions(obj, defs);

        // OpenAPI: generate one sample per top-level schema
        if (obj.SelectToken("components.schemas") is JObject compSchemas)
        {
            var result = new JObject();
            foreach (var prop in compSchemas.Properties())
            {
                if (prop.Value is JObject schemaObj)
                    result[prop.Name] = GenerateSampleToken(schemaObj, defs, new HashSet<string>(), 0);
            }
            return result.ToString(Formatting.Indented);
        }

        // Schema fragment: generate one sample per root definition
        if (IsSchemaFragment(obj))
        {
            var result = new JObject();
            foreach (var prop in obj.Properties())
            {
                if (prop.Value is JObject schemaObj)
                    result[prop.Name] = GenerateSampleToken(schemaObj, defs, new HashSet<string>(), 0);
            }
            return result.ToString(Formatting.Indented);
        }

        // Standard schema: generate a single sample
        var token = GenerateSampleToken(obj, defs, new HashSet<string>(), 0);
        return token.ToString(Formatting.Indented);
    }

    private static JToken GenerateSampleToken(
        JObject schema, Dictionary<string, JObject> defs,
        HashSet<string> visited, int depth)
    {
        if (depth > MaxDepth) return new JValue("…");

        // Follow $ref
        var refToken = schema["$ref"];
        if (refToken is not null)
        {
            string refPath = refToken.ToString();
            if (visited.Contains(refPath))
                return new JValue("(recursive)");

            var resolved = ResolveRef(refPath, defs);
            if (resolved is null) return new JValue("(unresolved $ref)");

            var newVisited = new HashSet<string>(visited) { refPath };
            return GenerateSampleToken(resolved, defs, newVisited, depth);
        }

        // enum: use first value
        if (schema["enum"] is JArray enumArr && enumArr.Count > 0)
            return enumArr[0].DeepClone();

        // const: use value
        if (schema["const"] is JToken constVal)
            return constVal.DeepClone();

        // allOf: merge and generate
        if (schema["allOf"] is JArray allOfArr)
        {
            var merged = new JObject();
            foreach (var sub in allOfArr)
            {
                if (sub is not JObject subObj) continue;
                var resolved = subObj["$ref"] is not null
                    ? ResolveRef(subObj["$ref"]!.ToString(), defs) ?? subObj
                    : subObj;
                if (resolved is not JObject resolvedObj) continue;
                var sample = GenerateSampleToken(resolvedObj, defs, visited, depth + 1);
                if (sample is JObject sampleObj)
                {
                    foreach (var p in sampleObj.Properties())
                        merged[p.Name] = p.Value;
                }
            }
            return merged;
        }

        // anyOf / oneOf: use first option
        JArray? choiceArr = schema["anyOf"] as JArray ?? schema["oneOf"] as JArray;
        if (choiceArr is not null && choiceArr.Count > 0 && choiceArr[0] is JObject firstChoice)
            return GenerateSampleToken(firstChoice, defs, visited, depth + 1);

        string type = DetectType(schema);
        // Handle union types ("string | null") — use the first concrete type
        if (type.Contains('|'))
            type = type.Split('|')[0].Trim();

        return type switch
        {
            "object" => GenerateSampleObject(schema, defs, visited, depth),
            "array" => GenerateSampleArray(schema, defs, visited, depth),
            "string" => new JValue(GetSampleStringValue(schema)),
            "number" => new JValue(GetSampleNumberValue(schema)),
            "integer" => new JValue(GetSampleIntegerValue(schema)),
            "boolean" => new JValue(true),
            "null" => JValue.CreateNull(),
            _ when schema["properties"] is not null => GenerateSampleObject(schema, defs, visited, depth),
            _ when schema["items"] is not null => GenerateSampleArray(schema, defs, visited, depth),
            _ => new JValue("sample")
        };
    }

    private static JObject GenerateSampleObject(
        JObject schema, Dictionary<string, JObject> defs,
        HashSet<string> visited, int depth)
    {
        var result = new JObject();
        if (schema["properties"] is JObject props)
        {
            foreach (var prop in props.Properties())
            {
                if (prop.Value is JObject propSchema)
                    result[prop.Name] = GenerateSampleToken(propSchema, defs, visited, depth + 1);
            }
        }
        return result;
    }

    private static JArray GenerateSampleArray(
        JObject schema, Dictionary<string, JObject> defs,
        HashSet<string> visited, int depth)
    {
        var arr = new JArray();
        if (schema["items"] is JObject itemsSchema)
            arr.Add(GenerateSampleToken(itemsSchema, defs, visited, depth + 1));
        return arr;
    }

    private static string GetSampleStringValue(JObject schema)
    {
        if (schema["default"] is JToken def) return def.ToString();
        if (schema["example"] is JToken ex) return ex.ToString();
        if (schema["examples"] is JArray exArr && exArr.Count > 0) return exArr[0].ToString();
        var format = schema["format"]?.ToString();
        return format switch
        {
            "date-time" => "2001-12-17T09:30:47Z",
            "date" => "2001-12-17",
            "time" => "09:30:47Z",
            "email" => "user@example.com",
            "uri" or "url" => "https://example.com",
            "uuid" => "550e8400-e29b-41d4-a716-446655440000",
            "ipv4" => "192.168.1.1",
            "ipv6" => "::1",
            "hostname" => "example.com",
            _ => "String"
        };
    }

    private static double GetSampleNumberValue(JObject schema)
    {
        if (schema["default"] is JToken def && double.TryParse(def.ToString(), out double d)) return d;
        if (schema["minimum"] is JToken min && double.TryParse(min.ToString(), out double mn)) return mn;
        return 0.0;
    }

    private static long GetSampleIntegerValue(JObject schema)
    {
        if (schema["default"] is JToken def && long.TryParse(def.ToString(), out long d)) return d;
        if (schema["minimum"] is JToken min && long.TryParse(min.ToString(), out long mn)) return mn;
        return 0;
    }

    /// <summary>
    /// Returns (totalProperties, objects, arrays) from a parsed schema tree.
    /// </summary>
    public static (int Properties, int Objects, int Arrays) GetStatistics(
        List<SchemaNode> roots)
    {
        int properties = 0, objects = 0, arrays = 0;
        CountNodes(roots, ref properties, ref objects, ref arrays);
        return (properties, objects, arrays);
    }

    // ──────────────────────────── Node Building ────────────────────────────

    private static SchemaNode BuildNode(
        string name,
        JObject schema,
        bool isRequired,
        int depth,
        HashSet<string> visited,
        Dictionary<string, JObject> defs,
        bool isChoice = false,
        int? choiceGroup = null,
        int? choiceOption = null,
        string? choiceKeyword = null)
    {
        if (depth > MaxDepth)
        {
            return new SchemaNode
            {
                Name = name,
                TypeName = "…",
                IsRequired = isRequired,
                IsTruncated = true,
                Documentation = "Max depth reached",
                IsExpanded = false
            };
        }

        // Follow $ref
        var refToken = schema["$ref"];
        if (refToken is not null)
        {
            string refPath = refToken.ToString();
            var resolved = ResolveRef(refPath, defs);
            if (resolved is null)
            {
                return new SchemaNode
                {
                    Name = name,
                    TypeName = "$ref",
                    Documentation = refPath,
                    IsRequired = isRequired,
                    IsExpanded = false
                };
            }

            if (visited.Contains(refPath))
            {
                return new SchemaNode
                {
                    Name = name,
                    TypeName = "⟳ recursive",
                    Documentation = $"References {refPath}",
                    IsRequired = isRequired,
                    IsRecursive = true,
                    IsExpanded = false
                };
            }

            var newVisited = new HashSet<string>(visited) { refPath };
            return BuildNode(name, resolved, isRequired, depth, newVisited, defs,
                isChoice, choiceGroup, choiceOption, choiceKeyword);
        }

        string type = DetectType(schema);
        string? description = CombineDocumentation(
            schema["title"]?.ToString(),
            schema["description"]?.ToString());
        string? format = schema["format"]?.ToString();
        var restrictions = ExtractRestrictions(schema);

        var children = new List<SchemaNode>();
        var choiceCounter = new int[] { 0 }; // mutable counter

        // Object properties
        if (schema["properties"] is JObject props)
        {
            var requiredSet = new HashSet<string>();
            if (schema["required"] is JArray reqArr)
                foreach (var r in reqArr)
                    requiredSet.Add(r.ToString());

            foreach (var prop in props.Properties())
            {
                if (prop.Value is JObject propSchema)
                {
                    var child = BuildNode(prop.Name, propSchema,
                        isRequired: requiredSet.Contains(prop.Name),
                        depth: depth + 1, visited: visited, defs: defs);
                    children.Add(child);
                }
            }
        }

        // Array items
        if (schema["items"] is JObject itemsSchema)
        {
            var itemNode = BuildNode("items[]", itemsSchema, isRequired: false,
                depth: depth + 1, visited: visited, defs: defs);

            string itemMinOccurs = schema["minItems"]?.ToString() ?? "0";
            string itemMaxOccurs = schema["maxItems"]?.ToString() ?? "unbounded";

            // Rebuild with correct occurs and IsArrayItem flag
            var arrayItemNode = new SchemaNode
            {
                Name = itemNode.Name,
                TypeName = itemNode.TypeName,
                TypeKind = itemNode.TypeKind,
                MinOccurs = itemMinOccurs,
                MaxOccurs = itemMaxOccurs,
                IsRequired = itemMinOccurs != "0",
                IsArrayItem = true,
                Documentation = itemNode.Documentation,
                Restrictions = itemNode.Restrictions,
                Format = itemNode.Format,
                IsRecursive = itemNode.IsRecursive,
                IsTruncated = itemNode.IsTruncated,
                IsExpanded = itemNode.IsExpanded
            };
            foreach (var c in itemNode.Children)
                arrayItemNode.Children.Add(c);
            children.Add(arrayItemNode);
        }

        // allOf — merge properties inline
        if (schema["allOf"] is JArray allOfArr)
        {
            foreach (var sub in allOfArr)
            {
                if (sub is not JObject subObj) continue;
                var resolved = subObj["$ref"] is not null
                    ? ResolveRef(subObj["$ref"]!.ToString(), defs) ?? subObj
                    : subObj;
                if (resolved is not JObject resolvedObj) continue;

                if (resolvedObj["properties"] is JObject subProps)
                {
                    var reqSet = new HashSet<string>();
                    if (resolvedObj["required"] is JArray reqArr)
                        foreach (var r in reqArr) reqSet.Add(r.ToString());

                    foreach (var prop in subProps.Properties())
                    {
                        if (prop.Value is JObject propSchema)
                        {
                            var child = BuildNode(prop.Name, propSchema,
                                isRequired: reqSet.Contains(prop.Name),
                                depth: depth + 1, visited: visited, defs: defs);
                            children.Add(child);
                        }
                    }
                }
            }
        }

        // anyOf / oneOf — choice groups
        JArray? choiceArr = schema["anyOf"] as JArray ?? schema["oneOf"] as JArray;
        string? keyword = schema["anyOf"] is not null ? "anyOf"
            : schema["oneOf"] is not null ? "oneOf" : null;
        if (choiceArr is not null && keyword is not null)
        {
            int groupId = choiceCounter[0]++;
            for (int i = 0; i < choiceArr.Count; i++)
            {
                if (choiceArr[i] is not JObject optObj) continue;
                string optName = optObj["title"]?.ToString() ?? $"Option {i + 1}";
                var optNode = BuildNode(optName, optObj, isRequired: false,
                    depth: depth + 1, visited: visited, defs: defs,
                    isChoice: true, choiceGroup: groupId, choiceOption: i,
                    choiceKeyword: keyword);
                children.Add(optNode);
            }
        }

        string typeKind = type switch
        {
            "object" => "object",
            "array" => "array",
            "string" => "string",
            "number" or "integer" => "number",
            "boolean" => "boolean",
            _ => type
        };

        var node = new SchemaNode
        {
            Name = name,
            TypeName = type,
            TypeKind = typeKind,
            MinOccurs = isRequired ? "1" : "0",
            MaxOccurs = "1",
            IsRequired = isRequired,
            IsChoice = isChoice,
            ChoiceGroup = choiceGroup,
            ChoiceOption = choiceOption,
            ChoiceKeyword = choiceKeyword,
            Documentation = description,
            Restrictions = restrictions,
            Format = format,
            IsExpanded = depth <= 1
        };

        foreach (var child in children)
            node.Children.Add(child);

        return node;
    }

    // ──────────────────────────── $ref Resolution ────────────────────────────

    private static JObject? ResolveRef(string refPath, Dictionary<string, JObject> defs)
    {
        if (string.IsNullOrEmpty(refPath) || !refPath.StartsWith('#')) return null;

        // #/$defs/Name or #/definitions/Name
        var match = System.Text.RegularExpressions.Regex.Match(
            refPath, @"^#/(?:\$defs|definitions)/(.+)$");
        if (match.Success)
            return defs.GetValueOrDefault(match.Groups[1].Value);

        // #/components/schemas/Name (OpenAPI)
        match = System.Text.RegularExpressions.Regex.Match(
            refPath, @"^#/components/schemas/(.+)$");
        if (match.Success)
            return defs.GetValueOrDefault(match.Groups[1].Value);

        // Bare fragment #/Name (last-resort)
        match = System.Text.RegularExpressions.Regex.Match(refPath, @"^#/(.+)$");
        if (match.Success)
            return defs.GetValueOrDefault(match.Groups[1].Value);

        return null;
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static JObject? ParseToJObject(string text)
    {
        string trimmed = text.TrimStart('\uFEFF').Trim();
        if (trimmed.Length == 0) return null;

        // Try JSON first
        if (trimmed[0] is '{' or '[')
        {
            try
            {
                var token = JToken.Parse(trimmed);
                return token as JObject;
            }
            catch { }
        }

        // Try YAML
        try
        {
            string json = YamlService.ConvertYamlToJson(trimmed);
            var token = JToken.Parse(json);
            return token as JObject;
        }
        catch { }

        return null;
    }

    private static void CollectDefinitions(JObject obj, Dictionary<string, JObject> defs)
    {
        // Standard JSON Schema defs
        foreach (string key in new[] { "$defs", "definitions" })
        {
            if (obj[key] is JObject defsObj)
            {
                foreach (var prop in defsObj.Properties())
                    if (prop.Value is JObject val)
                        defs.TryAdd(prop.Name, val);
            }
        }

        // OpenAPI components.schemas
        if (obj.SelectToken("components.schemas") is JObject compSchemas)
        {
            foreach (var prop in compSchemas.Properties())
                if (prop.Value is JObject val)
                    defs.TryAdd(prop.Name, val);
        }

        // Schema fragment
        if (IsSchemaFragment(obj))
        {
            foreach (var prop in obj.Properties())
                if (prop.Value is JObject val)
                    defs.TryAdd(prop.Name, val);
        }
    }

    private static bool IsSchemaFragment(JObject obj)
    {
        // A root JSON Schema or OpenAPI document would have these keys
        string[] schemaKeys = ["$schema", "openapi", "swagger", "info",
            "type", "properties", "allOf", "anyOf", "oneOf",
            "$defs", "definitions", "$ref"];

        foreach (var key in schemaKeys)
            if (obj[key] is not null)
                return false;

        // Treat as fragment if every value is an object that looks like a schema
        var props = obj.Properties().ToList();
        if (props.Count == 0) return false;

        return props.All(p => p.Value is JObject v &&
            (v["type"] is not null || v["properties"] is not null ||
             v["$ref"] is not null || v["allOf"] is not null ||
             v["anyOf"] is not null || v["oneOf"] is not null ||
             v["enum"] is not null));
    }

    private static string DetectType(JObject schema)
    {
        if (schema["$ref"] is not null) return "$ref";
        var typeToken = schema["type"];
        if (typeToken is not null)
        {
            if (typeToken is JArray arr)
                return string.Join(" | ", arr.Select(t => t.ToString()));
            return typeToken.ToString();
        }
        if (schema["enum"] is not null) return "enum";
        if (schema["anyOf"] is not null) return "anyOf";
        if (schema["oneOf"] is not null) return "oneOf";
        if (schema["allOf"] is not null) return "allOf";
        if (schema["properties"] is not null) return "object";
        if (schema["items"] is not null) return "array";
        return "";
    }

    private static Dictionary<string, string> ExtractRestrictions(JObject schema)
    {
        var r = new Dictionary<string, string>();
        AddIfPresent(r, schema, "minLength");
        AddIfPresent(r, schema, "maxLength");
        if (schema["pattern"] is JToken pat) r["pattern"] = pat.ToString();
        if (schema["enum"] is JArray enumArr)
        {
            var values = enumArr.Select(v => v.ToString()).ToList();
            r["enumeration"] = values.Count <= 5
                ? string.Join(", ", values)
                : string.Join(", ", values.Take(5)) + "…";
        }
        AddIfPresent(r, schema, "minimum");
        AddIfPresent(r, schema, "maximum");
        AddIfPresent(r, schema, "exclusiveMinimum");
        AddIfPresent(r, schema, "exclusiveMaximum");
        if (schema["format"] is JToken fmt) r["format"] = fmt.ToString();
        AddIfPresent(r, schema, "minItems");
        AddIfPresent(r, schema, "maxItems");
        AddIfPresent(r, schema, "multipleOf");
        AddIfPresent(r, schema, "minProperties");
        AddIfPresent(r, schema, "maxProperties");
        return r;
    }

    private static void AddIfPresent(Dictionary<string, string> r, JObject schema, string key)
    {
        if (schema[key] is JToken val) r[key] = val.ToString();
    }

    private static string? CombineDocumentation(string? title, string? description)
    {
        if (title is not null && description is not null)
            return $"{title} — {description}";
        return title ?? description;
    }

    private static void CountNodes(
        List<SchemaNode> nodes, ref int properties, ref int objects, ref int arrays)
    {
        foreach (var node in nodes)
        {
            properties++;
            if (node.TypeKind == "object") objects++;
            if (node.TypeKind == "array") arrays++;
            CountNodes(node.Children, ref properties, ref objects, ref arrays);
        }
    }
}
