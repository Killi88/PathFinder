using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PathFinder.Services;

internal record DcsaSortResult(string SortedJson, string SchemaName, string ApiName, string ApiVersion);

internal static class DcsaService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // ──────────────────────────── public API ────────────────────────────

    /// <summary>
    /// Fetches DCSA OpenAPI schemas from the given SwaggerHub registry URLs,
    /// identifies which schema best matches the JSON text, and returns the
    /// JSON with all object properties recursively sorted to match schema field order.
    /// </summary>
    public static async Task<DcsaSortResult> SortJsonBySchemaAsync(
        string jsonText, IReadOnlyList<string> schemaUrls)
    {
        var parsed = JToken.Parse(jsonText); // throws on invalid JSON

        // Collect schemas from all configured APIs and cache spec results
        var allSchemas = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var apiSpecs = new List<(string BaseUrl, string Version, Dictionary<string, JObject> Schemas)>();

        foreach (var baseUrl in schemaUrls)
        {
            var (version, spec) = await FetchLatestSpecAsync(baseUrl).ConfigureAwait(false);
            var schemas = ExtractSchemas(spec);
            foreach (var (name, schema) in schemas)
                allSchemas.TryAdd(name, schema);
            apiSpecs.Add((baseUrl, version, schemas));
        }

        // Identify the schema — first try top-level, then try nested objects
        var (schemaName, matchedSchema, matchPath) = IdentifySchemaDeep(parsed, allSchemas);

        if (schemaName is null || matchedSchema is null)
            throw new InvalidOperationException(
                "Could not identify a matching DCSA schema for this JSON document. " +
                "Ensure the document matches a known DCSA message type.");

        // Determine which API the matched schema came from
        string matchedApi = string.Empty;
        string matchedVersion = string.Empty;
        foreach (var (baseUrl, version, schemas) in apiSpecs)
        {
            if (schemas.ContainsKey(schemaName))
            {
                matchedApi = ExtractApiName(baseUrl);
                matchedVersion = version;
                break;
            }
        }

        // Recursively sort — if schema matched a nested path, sort from there
        JToken sorted;
        if (matchPath is null)
        {
            // Schema matched at top level
            sorted = SortToken(parsed, matchedSchema, allSchemas);
        }
        else
        {
            // Schema matched a nested object — sort the nested part, rebuild the tree
            sorted = SortNestedMatch(parsed, matchPath, matchedSchema, allSchemas);
        }

        var result = sorted.ToString(Newtonsoft.Json.Formatting.Indented);
        return new DcsaSortResult(result, schemaName, matchedApi, matchedVersion);
    }

    // ──────────────────────────── SwaggerHub fetch ────────────────────────────

    private static async Task<(string Version, JObject Spec)> FetchLatestSpecAsync(string registryUrl)
    {
        // Step 1: GET the registry listing to find defaultVersion
        var registryJson = await FetchJsonAsync(registryUrl).ConfigureAwait(false);
        var defaultVersion = registryJson["defaultVersion"]?.ToString();

        if (string.IsNullOrEmpty(defaultVersion))
            throw new InvalidOperationException(
                $"Could not determine the latest version from {registryUrl}");

        // Step 2: GET the full OpenAPI spec for that version
        var specUrl = $"{registryUrl.TrimEnd('/')}/{defaultVersion}";
        var spec = await FetchJsonAsync(specUrl).ConfigureAwait(false);
        return (defaultVersion, spec);
    }

    private static async Task<JObject> FetchJsonAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JObject.Parse(content);
    }

    // ──────────────────────────── schema extraction ────────────────────────────

    private static Dictionary<string, JObject> ExtractSchemas(JObject spec)
    {
        var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var schemas = spec.SelectToken("components.schemas") as JObject;
        if (schemas is null) return result;

        foreach (var prop in schemas.Properties())
        {
            if (prop.Value is JObject schemaObj)
                result[prop.Name] = schemaObj;
        }
        return result;
    }

    // ──────────────────────────── schema identification ────────────────────────────

    private static (string? Name, JObject? Schema) IdentifySchema(
        JToken token,
        IDictionary<string, JObject> candidates,
        IDictionary<string, JObject> allSchemas)
    {
        if (token is not JObject obj) return (null, null);

        var userKeys = obj.Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (userKeys.Count == 0) return (null, null);

        string? bestName = null;
        JObject? bestSchema = null;
        double bestScore = 0;
        double bestNestedScore = -1;

        foreach (var (name, schema) in candidates)
        {
            var schemaProps = GetAllProperties(schema, allSchemas);
            if (schemaProps.Count == 0) continue;

            // Score: how many of the user's keys appear in the schema
            int matched = userKeys.Count(k => schemaProps.ContainsKey(k));
            double score = (double)matched / userKeys.Count;

            if (score > bestScore)
            {
                bestName = name;
                bestSchema = schema;
                bestScore = score;
                bestNestedScore = ComputeNestedScore(obj, schema, allSchemas);
            }
            else if (score == bestScore)
            {
                // Tie-break: prefer the schema whose nested property definitions
                // better match the user's nested objects (e.g. disambiguate CloudEvent
                // wrappers like TransportDocumentNotification vs ShippingInstructionsNotification)
                double nestedScore = ComputeNestedScore(obj, schema, allSchemas);
                if (nestedScore > bestNestedScore)
                {
                    bestName = name;
                    bestSchema = schema;
                    bestNestedScore = nestedScore;
                }
            }
        }

        // Require at least 40% match
        if (bestScore < 0.4) return (null, null);

        return (bestName, bestSchema);
    }

    /// <summary>
    /// Computes a secondary match score by checking how well each nested JObject
    /// in the user data matches the corresponding property's schema definition.
    /// Used to disambiguate schemas that share the same top-level property names.
    /// </summary>
    private static double ComputeNestedScore(
        JObject obj, JObject schema, IDictionary<string, JObject> allSchemas)
    {
        double totalScore = 0;
        int count = 0;

        foreach (var prop in obj.Properties())
        {
            if (prop.Value is not JObject nested) continue;

            var propSchema = ResolvePropertySchema(prop.Name, schema, allSchemas);
            if (propSchema is null) continue;

            var nestedKeys = nested.Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            if (nestedKeys.Count == 0) continue;

            var schemaProps = GetAllProperties(propSchema, allSchemas);
            if (schemaProps.Count == 0) continue;

            int matched = nestedKeys.Count(k => schemaProps.ContainsKey(k));
            totalScore += (double)matched / nestedKeys.Count;
            count++;
        }

        return count > 0 ? totalScore / count : 0;
    }

    /// <summary>
    /// Tries to identify a schema match at the top level first. If that fails,
    /// recursively searches nested JObject properties (up to 3 levels deep) for a match.
    /// Returns the match path (dot-separated property names) if found nested, or null for top-level.
    /// </summary>
    private static (string? Name, JObject? Schema, string? MatchPath) IdentifySchemaDeep(
        JToken token, IDictionary<string, JObject> allSchemas)
    {
        // Try top-level first
        var (name, schema) = IdentifySchema(token, allSchemas, allSchemas);
        if (name is not null)
            return (name, schema, null);

        // If top-level didn't match, try nested objects (up to 3 levels)
        if (token is JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                if (prop.Value is JObject nested)
                {
                    var (nestedName, nestedSchema) = IdentifySchema(nested, allSchemas, allSchemas);
                    if (nestedName is not null)
                        return (nestedName, nestedSchema, prop.Name);

                    // Try one level deeper
                    foreach (var innerProp in nested.Properties())
                    {
                        if (innerProp.Value is JObject innerNested)
                        {
                            var (innerName, innerSchema) = IdentifySchema(innerNested, allSchemas, allSchemas);
                            if (innerName is not null)
                                return (innerName, innerSchema, $"{prop.Name}.{innerProp.Name}");
                        }
                    }
                }
            }
        }

        return (null, null, null);
    }

    /// <summary>
    /// Sorts a nested portion of the document identified by matchPath (dot-separated),
    /// preserving the wrapper structure and sorting unmatched wrapper properties in original order.
    /// </summary>
    private static JToken SortNestedMatch(
        JToken root, string matchPath, JObject matchedSchema, IDictionary<string, JObject> allSchemas)
    {
        var parts = matchPath.Split('.');
        return SortAtPath(root, parts, 0, matchedSchema, allSchemas);
    }

    private static JToken SortAtPath(
        JToken token, string[] pathParts, int depth,
        JObject matchedSchema, IDictionary<string, JObject> allSchemas)
    {
        if (token is not JObject obj) return token.DeepClone();

        if (depth >= pathParts.Length)
        {
            // We've reached the target — sort using the matched schema
            return SortToken(obj, matchedSchema, allSchemas);
        }

        var targetProp = pathParts[depth];
        var result = new JObject();
        foreach (var prop in obj.Properties())
        {
            if (prop.Name == targetProp)
                result[prop.Name] = SortAtPath(prop.Value, pathParts, depth + 1, matchedSchema, allSchemas);
            else
                result[prop.Name] = SortToken(prop.Value, null, allSchemas); // sort sub-objects generically
        }
        return result;
    }

    // ──────────────────────────── recursive sorting ────────────────────────────

    private static JToken SortToken(JToken token, JObject? schema, IDictionary<string, JObject> allSchemas)
    {
        if (token is JObject obj)
        {
            var orderedProps = GetAllProperties(schema, allSchemas);
            var sorted = new JObject();

            // First: properties defined in the schema, in schema order
            foreach (var propName in orderedProps.Keys)
            {
                if (obj.TryGetValue(propName, out var value))
                {
                    var propSchema = ResolvePropertySchema(propName, schema, allSchemas);
                    sorted[propName] = SortToken(value, propSchema, allSchemas);
                }
            }

            // Then: properties NOT in the schema, in their original order
            foreach (var prop in obj.Properties())
            {
                if (!sorted.ContainsKey(prop.Name))
                {
                    sorted[prop.Name] = SortToken(prop.Value, null, allSchemas);
                }
            }

            return sorted;
        }

        if (token is JArray arr)
        {
            var itemSchema = ResolveArrayItemSchema(schema, allSchemas);
            var sorted = new JArray();
            foreach (var item in arr)
                sorted.Add(SortToken(item, itemSchema, allSchemas));
            return sorted;
        }

        return token.DeepClone();
    }

    // ──────────────────────────── schema helpers ────────────────────────────

    /// <summary>
    /// Returns an ordered dictionary of all property names defined in a schema,
    /// resolving $ref, allOf, oneOf, anyOf compositions.
    /// The insertion order reflects the field order defined in the schema.
    /// </summary>
    private static OrderedDictionary<string> GetAllProperties(JObject? schema, IDictionary<string, JObject> allSchemas)
    {
        var result = new OrderedDictionary<string>();
        if (schema is null) return result;

        CollectProperties(schema, allSchemas, result, new HashSet<string>(StringComparer.Ordinal));
        return result;
    }

    private static void CollectProperties(
        JObject schema,
        IDictionary<string, JObject> allSchemas,
        OrderedDictionary<string> result,
        HashSet<string> visited)
    {
        // Handle $ref
        var refPath = schema["$ref"]?.ToString();
        if (refPath is not null)
        {
            var resolved = ResolveRef(refPath, allSchemas);
            if (resolved is not null && visited.Add(refPath))
                CollectProperties(resolved, allSchemas, result, visited);
            return;
        }

        // Handle allOf / oneOf / anyOf — collect properties from all sub-schemas
        foreach (var keyword in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (schema[keyword] is JArray composites)
            {
                foreach (var item in composites)
                {
                    if (item is JObject subSchema)
                        CollectProperties(subSchema, allSchemas, result, visited);
                }
            }
        }

        // Collect direct properties
        if (schema["properties"] is JObject props)
        {
            foreach (var prop in props.Properties())
                result.TryAdd(prop.Name);
        }
    }

    /// <summary>
    /// Resolves a $ref like "#/components/schemas/Foo" to the JObject schema definition.
    /// </summary>
    private static JObject? ResolveRef(string refPath, IDictionary<string, JObject> allSchemas)
    {
        // Expected format: #/components/schemas/SchemaName
        const string prefix = "#/components/schemas/";
        if (!refPath.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var name = refPath[prefix.Length..];
        return allSchemas.TryGetValue(name, out var schema) ? schema : null;
    }

    /// <summary>
    /// Given a property name and its parent schema, resolves the property's own schema definition.
    /// </summary>
    private static JObject? ResolvePropertySchema(
        string propertyName, JObject? parentSchema, IDictionary<string, JObject> allSchemas)
    {
        if (parentSchema is null) return null;

        // Search direct properties
        var propDef = FindPropertyDefinition(propertyName, parentSchema, allSchemas);
        if (propDef is null) return null;

        // If it's a $ref, resolve it
        var refPath = propDef["$ref"]?.ToString();
        if (refPath is not null)
            return ResolveRef(refPath, allSchemas);

        return propDef;
    }

    private static JObject? FindPropertyDefinition(
        string propertyName, JObject schema, IDictionary<string, JObject> allSchemas)
    {
        // Handle $ref first
        var refPath = schema["$ref"]?.ToString();
        if (refPath is not null)
        {
            var resolved = ResolveRef(refPath, allSchemas);
            return resolved is not null ? FindPropertyDefinition(propertyName, resolved, allSchemas) : null;
        }

        // Check allOf/oneOf/anyOf
        foreach (var keyword in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (schema[keyword] is JArray composites)
            {
                foreach (var item in composites)
                {
                    if (item is JObject sub)
                    {
                        var found = FindPropertyDefinition(propertyName, sub, allSchemas);
                        if (found is not null) return found;
                    }
                }
            }
        }

        // Check direct properties
        if (schema["properties"] is JObject props && props[propertyName] is JObject propDef)
            return propDef;

        return null;
    }

    /// <summary>
    /// If the schema defines an array type, resolves the items schema.
    /// </summary>
    private static JObject? ResolveArrayItemSchema(JObject? schema, IDictionary<string, JObject> allSchemas)
    {
        if (schema is null) return null;

        // Handle $ref on the array schema itself
        var refPath = schema["$ref"]?.ToString();
        if (refPath is not null)
        {
            var resolved = ResolveRef(refPath, allSchemas);
            return resolved is not null ? ResolveArrayItemSchema(resolved, allSchemas) : null;
        }

        var items = schema["items"];
        if (items is null) return null;

        if (items is JObject itemObj)
        {
            var itemRef = itemObj["$ref"]?.ToString();
            if (itemRef is not null)
                return ResolveRef(itemRef, allSchemas);
            return itemObj;
        }

        return null;
    }

    private static string ExtractApiName(string url)
    {
        // "https://api.swaggerhub.com/apis/dcsaorg/DCSA_BKG" → "DCSA_BKG"
        var parts = url.TrimEnd('/').Split('/');
        return parts.Length > 0 ? parts[^1] : url;
    }

    // ──────────────────────────── ordered set helper ────────────────────────────

    /// <summary>
    /// A simple ordered set that preserves insertion order and supports ContainsKey lookups.
    /// Used to track schema property names in definition order.
    /// </summary>
    internal sealed class OrderedDictionary<T> where T : notnull
    {
        private readonly List<T> _order = new();
        private readonly HashSet<T> _set;

        public OrderedDictionary() => _set = new HashSet<T>(StringComparer.Ordinal as IEqualityComparer<T> ?? EqualityComparer<T>.Default);

        public IEnumerable<T> Keys => _order;
        public int Count => _order.Count;

        public bool TryAdd(T item)
        {
            if (_set.Add(item))
            {
                _order.Add(item);
                return true;
            }
            return false;
        }

        public bool ContainsKey(T item) => _set.Contains(item);
    }
}
