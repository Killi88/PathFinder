using PathFinder.Services;

namespace PathFinder.Tests;

public class JsonSchemaServiceTests
{
    // ──────────────────────────── Detection ────────────────────────────

    [Fact]
    public void IsJsonSchemaContent_StandardSchema_ReturnsTrue()
    {
        string json = """
            {
                "$schema": "https://json-schema.org/draft/2020-12/schema",
                "type": "object",
                "properties": { "name": { "type": "string" } }
            }
            """;
        Assert.True(JsonSchemaService.IsJsonSchemaContent(json));
    }

    [Fact]
    public void IsJsonSchemaContent_OpenApiSpec_ReturnsTrue()
    {
        string json = """
            {
                "openapi": "3.0.0",
                "info": { "title": "Test", "version": "1.0" },
                "paths": {},
                "components": { "schemas": { "Foo": { "type": "object" } } }
            }
            """;
        Assert.True(JsonSchemaService.IsJsonSchemaContent(json));
    }

    [Fact]
    public void IsJsonSchemaContent_SchemaWithDefs_ReturnsTrue()
    {
        string json = """
            {
                "type": "object",
                "$defs": { "Name": { "type": "string" } }
            }
            """;
        Assert.True(JsonSchemaService.IsJsonSchemaContent(json));
    }

    [Fact]
    public void IsJsonSchemaContent_PlainJson_ReturnsFalse()
    {
        string json = """{ "name": "John", "age": 30 }""";
        Assert.False(JsonSchemaService.IsJsonSchemaContent(json));
    }

    [Fact]
    public void IsJsonSchemaContent_EmptyString_ReturnsFalse()
    {
        Assert.False(JsonSchemaService.IsJsonSchemaContent(""));
    }

    [Fact]
    public void IsJsonSchemaContent_InvalidJson_ReturnsFalse()
    {
        Assert.False(JsonSchemaService.IsJsonSchemaContent("not json"));
    }

    [Fact]
    public void IsJsonSchemaContent_YamlOpenApi_ReturnsTrue()
    {
        string yaml = """
            openapi: "3.0.0"
            info:
              title: Test
              version: "1.0"
            paths: {}
            components:
              schemas:
                Foo:
                  type: object
            """;
        Assert.True(JsonSchemaService.IsJsonSchemaContent(yaml));
    }

    // ──────────────────────────── Parsing ────────────────────────────

    [Fact]
    public void ParseJsonSchema_SimpleObjectSchema_ReturnsOneRoot()
    {
        string json = """
            {
                "title": "Person",
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" }
                },
                "required": ["name"]
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        Assert.Single(roots);
        Assert.Equal("Person", roots[0].Name);
    }

    [Fact]
    public void ParseJsonSchema_SimpleObjectSchema_NameIsRequired()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" }
                },
                "required": ["name"]
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var nameChild = roots[0].Children.First(c => c.Name == "name");
        Assert.True(nameChild.IsRequired);
    }

    [Fact]
    public void ParseJsonSchema_SimpleObjectSchema_AgeIsOptional()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" }
                },
                "required": ["name"]
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var ageChild = roots[0].Children.First(c => c.Name == "age");
        Assert.False(ageChild.IsRequired);
    }

    [Fact]
    public void ParseJsonSchema_RefResolution_FollowsRef()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "address": { "$ref": "#/$defs/Address" }
                },
                "$defs": {
                    "Address": {
                        "type": "object",
                        "properties": {
                            "street": { "type": "string" }
                        }
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var addr = roots[0].Children.First(c => c.Name == "address");
        Assert.Equal("object", addr.TypeKind);
        Assert.Contains(addr.Children, c => c.Name == "street");
    }

    [Fact]
    public void ParseJsonSchema_RecursiveRef_MarksRecursive()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "child": { "$ref": "#/$defs/Node" }
                },
                "$defs": {
                    "Node": {
                        "type": "object",
                        "properties": {
                            "value": { "type": "string" },
                            "children": {
                                "type": "array",
                                "items": { "$ref": "#/$defs/Node" }
                            }
                        }
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var recursive = FindNodes(roots, n => n.IsRecursive);
        Assert.NotEmpty(recursive);
    }

    [Fact]
    public void ParseJsonSchema_ArrayType_HasArrayKind()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "tags": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var tags = roots[0].Children.First(c => c.Name == "tags");
        Assert.Equal("array", tags.TypeKind);
    }

    [Fact]
    public void ParseJsonSchema_ArrayType_HasArrayItemChild()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "tags": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var tags = roots[0].Children.First(c => c.Name == "tags");
        var itemChild = tags.Children.First(c => c.IsArrayItem);
        Assert.Equal("string", itemChild.TypeKind);
    }

    [Fact]
    public void ParseJsonSchema_OneOfChoice_MarksChoiceNodes()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "value": {
                        "oneOf": [
                            { "type": "string", "title": "StringVal" },
                            { "type": "number", "title": "NumberVal" }
                        ]
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var value = roots[0].Children.First(c => c.Name == "value");
        var choices = value.Children.Where(c => c.IsChoice).ToList();
        Assert.Equal(2, choices.Count);
    }

    [Fact]
    public void ParseJsonSchema_AllOfMerge_MergesProperties()
    {
        string json = """
            {
                "type": "object",
                "allOf": [
                    {
                        "type": "object",
                        "properties": { "a": { "type": "string" } }
                    },
                    {
                        "type": "object",
                        "properties": { "b": { "type": "integer" } }
                    }
                ]
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        Assert.Contains(roots[0].Children, c => c.Name == "a");
        Assert.Contains(roots[0].Children, c => c.Name == "b");
    }

    [Fact]
    public void ParseJsonSchema_Restrictions_ExtractsEnumeration()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "color": {
                        "type": "string",
                        "enum": ["red", "green", "blue"]
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var color = roots[0].Children.First(c => c.Name == "color");
        Assert.Contains("enumeration", color.Restrictions.Keys);
    }

    [Fact]
    public void ParseJsonSchema_OpenApiSpec_ParsesSchemasAsRoots()
    {
        string json = """
            {
                "openapi": "3.0.0",
                "info": { "title": "Test", "version": "1.0" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Pet": {
                            "type": "object",
                            "properties": {
                                "name": { "type": "string" }
                            }
                        },
                        "Error": {
                            "type": "object",
                            "properties": {
                                "message": { "type": "string" }
                            }
                        }
                    }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, r => r.Name == "Pet");
        Assert.Contains(roots, r => r.Name == "Error");
    }

    [Fact]
    public void ParseJsonSchema_EmptyInput_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JsonSchemaService.ParseJsonSchema(""));
    }

    [Fact]
    public void ParseJsonSchema_Description_ExtractsDocumentation()
    {
        string json = """
            {
                "type": "object",
                "description": "A person record",
                "properties": {
                    "name": { "type": "string", "description": "Full name" }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        Assert.Contains("A person record", roots[0].Documentation!);
        var name = roots[0].Children.First(c => c.Name == "name");
        Assert.Equal("Full name", name.Documentation);
    }

    // ──────────────────────────── Statistics ────────────────────────────

    [Fact]
    public void GetStatistics_SimpleSchema_ReturnsNonZeroCounts()
    {
        string json = """
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "items": { "type": "array", "items": { "type": "string" } }
                }
            }
            """;
        var roots = JsonSchemaService.ParseJsonSchema(json);
        var stats = JsonSchemaService.GetStatistics(roots);
        Assert.True(stats.Properties > 0);
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static List<PathFinder.Models.SchemaNode> FindNodes(
        List<PathFinder.Models.SchemaNode> nodes,
        Func<PathFinder.Models.SchemaNode, bool> predicate)
    {
        var result = new List<PathFinder.Models.SchemaNode>();
        foreach (var n in nodes)
        {
            if (predicate(n)) result.Add(n);
            result.AddRange(FindNodes(n.Children, predicate));
        }
        return result;
    }
}
