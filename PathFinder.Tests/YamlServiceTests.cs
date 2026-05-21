using System.IO;
using PathFinder.Services;

namespace PathFinder.Tests;

public class YamlServiceTests
{
    // Line numbers (1-based) for example.yaml:
    //  1: store:
    //  2:   name: John
    //  3:   age: 30
    //  4:   active: true
    //  5:   books:
    //  6:     - title: foo
    //  7:       price: 9.99
    //  8:     - title: bar
    //  9:       price: 12.50
    // 10:   tags:
    // 11:     - fiction
    // 12:     - bestseller
    // 13: metadata:
    // 14:   created: 2024-01-15
    // 15:   version: null

    private const string SampleYaml =
        "store:\n" +
        "  name: John\n" +
        "  age: 30\n" +
        "  active: true\n" +
        "  books:\n" +
        "    - title: foo\n" +
        "      price: 9.99\n" +
        "    - title: bar\n" +
        "      price: 12.50\n" +
        "  tags:\n" +
        "    - fiction\n" +
        "    - bestseller\n" +
        "metadata:\n" +
        "  created: 2024-01-15\n" +
        "  version: null";

    // ── Format ────────────────────────────────────────────────────────────

    [Fact]
    public void FormatYaml_ValidYaml_ParsesAndFormatsWithoutError()
    {
        var result = YamlService.FormatYaml(SampleYaml);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void FormatYaml_ExampleFile_ParsesAndFormatsWithoutError()
    {
        var yaml = File.ReadAllText(TestFilePath("example.yaml"));
        var result = YamlService.FormatYaml(yaml);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void FormatYaml_Idempotent_ProducesSameOutput()
    {
        var first = YamlService.FormatYaml(SampleYaml);
        var second = YamlService.FormatYaml(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void FormatYaml_IncorrectSyntax_ThrowsException()
    {
        var yaml = File.ReadAllText(TestFilePath("incorrectSyntax.yaml"));
        Assert.ThrowsAny<Exception>(() => YamlService.FormatYaml(yaml));
    }

    [Fact]
    public void FormatYaml_EmptyString_ReturnsEmpty()
    {
        var result = YamlService.FormatYaml("");
        Assert.Equal("", result);
    }

    [Fact]
    public void FormatYaml_BadIndentation_NormalizesTo2Spaces()
    {
        // 4-space indentation should be normalised to 2-space
        var badYaml = "store:\n    name: John\n    age: 30\n    books:\n        - title: foo\n        - title: bar";
        var result = YamlService.FormatYaml(badYaml);
        Assert.Contains("  name: John", result);
        Assert.Contains("  age: 30", result);
        Assert.Contains("  - title: foo", result);
        Assert.DoesNotContain("    name", result);
    }

    [Fact]
    public void FormatYaml_TabIndentation_NormalizesToSpaces()
    {
        var tabYaml = "store:\n\tname: John\n\tage: 30";
        var result = YamlService.FormatYaml(tabYaml);
        Assert.Contains("  name: John", result);
        Assert.Contains("  age: 30", result);
        Assert.DoesNotContain("\t", result);
    }

    [Fact]
    public void FormatYaml_MultiDocument_PreservesDocuments()
    {
        var yaml = "---\nname: first\n---\nname: second";
        var result = YamlService.FormatYaml(yaml);
        Assert.Contains("---", result);
        Assert.Contains("first", result);
        Assert.Contains("second", result);
    }

    // ── GetYamlPathAtLine ──────────────────────────────────────────────────

    [Fact]
    public void GetYamlPathAtLine_RootMapping_ReturnsDollar()
    {
        var result = YamlService.GetYamlPathAtLine(SampleYaml, 1);
        Assert.Equal("$", result);
    }

    [Fact]
    public void GetYamlPathAtLine_NestedProperty_ReturnsPath()
    {
        var result = YamlService.GetYamlPathAtLine(SampleYaml, 2);
        Assert.Equal("$.store.name", result);
    }

    [Fact]
    public void GetYamlPathAtLine_ArrayElement_ReturnsIndexedPath()
    {
        var result = YamlService.GetYamlPathAtLine(SampleYaml, 6);
        Assert.Contains("books", result);
    }

    [Fact]
    public void GetYamlPathAtLine_InvalidYaml_ReturnsNull()
    {
        var result = YamlService.GetYamlPathAtLine("key: [invalid\nunclosed: [", 1);
        Assert.Null(result);
    }

    // ── ExecuteYamlPath ────────────────────────────────────────────────────

    [Fact]
    public void ExecuteYamlPath_RootPath_ReturnsOneResult()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$");
        Assert.Single(results);
        Assert.Equal("$", results[0].XPath);
    }

    [Fact]
    public void ExecuteYamlPath_ScalarProperty_ReturnsCorrectValue()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store.name");
        Assert.Single(results);
        Assert.Equal("John", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_NumericProperty_ReturnsCorrectValue()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store.age");
        Assert.Single(results);
        Assert.Equal("30", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_ArrayIndex_ReturnsCorrectValue()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store.books[0].title");
        Assert.Single(results);
        Assert.Equal("foo", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_SecondArrayIndex_ReturnsCorrectValue()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store.books[1].title");
        Assert.Single(results);
        Assert.Equal("bar", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_ObjectNode_PreviewIsEllipsis()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store");
        Assert.Single(results);
        Assert.Equal("{…}", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_ArrayNode_PreviewIsEllipsis()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.store.books");
        Assert.Single(results);
        Assert.Equal("[…]", results[0].Preview);
    }

    [Fact]
    public void ExecuteYamlPath_NullValue_ReturnsResult()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.metadata.version");
        Assert.Single(results);
    }

    [Fact]
    public void ExecuteYamlPath_NonExistentPath_ReturnsEmpty()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.nonexistent");
        Assert.Empty(results);
    }

    [Fact]
    public void ExecuteYamlPath_WildcardProperty_ReturnsAllChildren()
    {
        var results = YamlService.ExecuteYamlPath(SampleYaml, "$.*");
        Assert.Equal(2, results.Count); // store, metadata
    }

    [Fact]
    public void ExecuteYamlPath_InvalidExpression_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            YamlService.ExecuteYamlPath(SampleYaml, "not-a-path"));
    }

    [Fact]
    public void ExecuteYamlPath_InvalidYaml_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            YamlService.ExecuteYamlPath("key: [invalid\nunclosed: [", "$.foo"));
    }

    // ── GetAllPaths ────────────────────────────────────────────────────────

    [Fact]
    public void GetAllPaths_SampleYaml_ContainsRootPath()
    {
        var results = YamlService.GetAllPaths(SampleYaml);
        var paths = results.Select(r => r.XPath).ToList();
        Assert.Contains("$", paths);
    }

    [Fact]
    public void GetAllPaths_SampleYaml_ContainsNestedPaths()
    {
        var results = YamlService.GetAllPaths(SampleYaml);
        var paths = results.Select(r => r.XPath).ToList();
        Assert.Contains("$.store.name", paths);
        Assert.Contains("$.store.books", paths);
        Assert.Contains("$.metadata.created", paths);
    }

    [Fact]
    public void GetAllPaths_SampleYaml_ContainsArrayElementPaths()
    {
        var results = YamlService.GetAllPaths(SampleYaml);
        var paths = results.Select(r => r.XPath).ToList();
        Assert.Contains("$.store.tags[0]", paths);
        Assert.Contains("$.store.tags[1]", paths);
    }

    [Fact]
    public void GetAllPaths_SampleYaml_NamePreviewIsJohn()
    {
        var results = YamlService.GetAllPaths(SampleYaml);
        var nameItem = results.First(r => r.XPath == "$.store.name");
        Assert.Equal("John", nameItem.Preview);
    }

    [Fact]
    public void GetAllPaths_InvalidYaml_ReturnsEmptyList()
    {
        var results = YamlService.GetAllPaths("key: [invalid\nunclosed: [");
        Assert.Empty(results);
    }

    // ── ConvertYamlToJson ──────────────────────────────────────────────────

    [Fact]
    public void ConvertYamlToJson_ValidYaml_ProducesValidJson()
    {
        var json = YamlService.ConvertYamlToJson(SampleYaml);
        Assert.False(string.IsNullOrWhiteSpace(json));
        // Should be valid JSON
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ConvertYamlToJson_PreservesStringValue()
    {
        var json = YamlService.ConvertYamlToJson(SampleYaml);
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.Equal("John", parsed["store"]?["name"]?.ToString());
    }

    [Fact]
    public void ConvertYamlToJson_PreservesNumericValue()
    {
        var json = YamlService.ConvertYamlToJson(SampleYaml);
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.Equal(30, (int?)parsed["store"]?["age"]);
    }

    [Fact]
    public void ConvertYamlToJson_PreservesArrayLength()
    {
        var json = YamlService.ConvertYamlToJson(SampleYaml);
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.Equal(2, ((Newtonsoft.Json.Linq.JArray?)parsed["store"]?["books"])?.Count);
    }

    // ── ConvertJsonToYaml ──────────────────────────────────────────────────

    [Fact]
    public void ConvertJsonToYaml_ValidJson_ProducesValidYaml()
    {
        var json = "{\"name\": \"Alice\", \"age\": 25}";
        var yaml = YamlService.ConvertJsonToYaml(json);
        Assert.False(string.IsNullOrWhiteSpace(yaml));
        Assert.Contains("name: Alice", yaml);
        Assert.Contains("age: 25", yaml);
    }

    [Fact]
    public void ConvertJsonToYaml_RoundTrip_PreservesData()
    {
        var json = YamlService.ConvertYamlToJson(SampleYaml);
        var yaml = YamlService.ConvertJsonToYaml(json);
        // Re-converting back to JSON should give equivalent data
        var json2 = YamlService.ConvertYamlToJson(yaml);
        var t1 = Newtonsoft.Json.Linq.JToken.Parse(json);
        var t2 = Newtonsoft.Json.Linq.JToken.Parse(json2);
        Assert.True(Newtonsoft.Json.Linq.JToken.DeepEquals(t1, t2));
    }

    [Fact]
    public void ConvertJsonToYaml_Array_ProducesSequence()
    {
        var json = "[1, 2, 3]";
        var yaml = YamlService.ConvertJsonToYaml(json);
        Assert.Contains("- 1", yaml);
        Assert.Contains("- 2", yaml);
        Assert.Contains("- 3", yaml);
    }

    private static string TestFilePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", filename);
}
