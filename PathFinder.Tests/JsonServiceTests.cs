using System.IO;
using System.Linq;
using PathFinder.Services;

namespace PathFinder.Tests;

public class JsonServiceTests
{
    // Line numbers (1-based):
    //  1: {
    //  2:     "store": {
    //  3:         "name": "John",
    //  4:         "age": 30,
    //  5:         "books": [
    //  6:             {
    //  7:                 "title": "foo"
    //  8:             },
    //  9:             {
    // 10:                 "title": "bar"
    // 11:             }
    // 12:         ]
    // 13:     }
    // 14: }
    private const string SampleJson =
        "{\n" +
        "    \"store\": {\n" +
        "        \"name\": \"John\",\n" +
        "        \"age\": 30,\n" +
        "        \"books\": [\n" +
        "            {\n" +
        "                \"title\": \"foo\"\n" +
        "            },\n" +
        "            {\n" +
        "                \"title\": \"bar\"\n" +
        "            }\n" +
        "        ]\n" +
        "    }\n" +
        "}";

    [Fact]
    public void GetJsonPathAtLine_RootObject_ReturnsDollar()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 1);
        Assert.Equal("$", result);
    }

    [Fact]
    public void GetJsonPathAtLine_TopLevelProperty_ReturnsPath()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 2);
        Assert.Equal("$.store", result);
    }

    [Fact]
    public void GetJsonPathAtLine_NestedProperty_ReturnsPath()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 3);
        Assert.Equal("$.store.name", result);
    }

    [Fact]
    public void GetJsonPathAtLine_NumberProperty_ReturnsPath()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 4);
        Assert.Equal("$.store.age", result);
    }

    [Fact]
    public void GetJsonPathAtLine_ArrayItem_ReturnsPath()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 7);
        Assert.Equal("$.store.books[0].title", result);
    }

    [Fact]
    public void GetJsonPathAtLine_SecondArrayItem_ReturnsPath()
    {
        var result = JsonService.GetJsonPathAtLine(SampleJson, 10);
        Assert.Equal("$.store.books[1].title", result);
    }

    // ── Root array ─────────────────────────────────────────────────────────

    // Line numbers (1-based):
    //  1: [
    //  2:     {
    //  3:         "title": "foo"
    //  4:     },
    //  5:     {
    //  6:         "title": "bar"
    //  7:     }
    //  8: ]
    private const string RootArrayJson =
        "[\n" +
        "    {\n" +
        "        \"title\": \"foo\"\n" +
        "    },\n" +
        "    {\n" +
        "        \"title\": \"bar\"\n" +
        "    }\n" +
        "]";

    [Fact]
    public void GetJsonPathAtLine_RootArray_ReturnsDollar()
    {
        var result = JsonService.GetJsonPathAtLine(RootArrayJson, 1);
        Assert.Equal("$", result);
    }

    [Fact]
    public void GetJsonPathAtLine_RootArrayFirstElement_ReturnsIndexedPath()
    {
        var result = JsonService.GetJsonPathAtLine(RootArrayJson, 2);
        Assert.Equal("$[0]", result);
    }

    [Fact]
    public void GetJsonPathAtLine_RootArrayFirstElementProperty_ReturnsIndexedPath()
    {
        var result = JsonService.GetJsonPathAtLine(RootArrayJson, 3);
        Assert.Equal("$[0].title", result);
    }

    [Fact]
    public void GetJsonPathAtLine_RootArraySecondElementProperty_ReturnsIndexedPath()
    {
        var result = JsonService.GetJsonPathAtLine(RootArrayJson, 6);
        Assert.Equal("$[1].title", result);
    }

    // ── Format / Indentation ──────────────────────────────────────────────
    //
    // example.json is 2-space indented; FormatJson should normalise to 4-space.
    //
    // example.json structure:
    //  { "TargetDeserializationType": "…", "Sender": "MAERSK",
    //    "JsonContent": { "specversion": "1.0", … } }

    [Fact]
    public void FormatJson_ExampleJson_ParsesAndFormatsWithoutError()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var result = JsonService.FormatJson(json);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void FormatJson_ExampleJson_TopLevelPropertiesAreFourSpacesIndented()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var result = JsonService.FormatJson(json);
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Top-level keys → exactly 4 leading spaces
        Assert.Contains(lines, l => l.StartsWith("    \"Sender\""));
        Assert.Contains(lines, l => l.StartsWith("    \"JsonContent\""));
    }

    [Fact]
    public void FormatJson_ExampleJson_NestedPropertiesAreEightSpacesIndented()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var result = JsonService.FormatJson(json);
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Properties inside "JsonContent" → 8 leading spaces
        Assert.Contains(lines, l => l.StartsWith("        \"specversion\""));
        Assert.Contains(lines, l => l.StartsWith("        \"id\""));
    }

    // ── GetJsonPathAtLine – invalid / edge cases ───────────────────────────

    [Fact]
    public void GetJsonPathAtLine_InvalidJson_ReturnsNull()
    {
        var result = JsonService.GetJsonPathAtLine("{invalid}", 1);
        Assert.Null(result);
    }

    // ── JSONPath execution ────────────────────────────────────────────────

    [Fact]
    public void ExecuteJsonPath_ExampleJson_FindsSender()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var results = JsonService.ExecuteJsonPath(json, "$.Sender");

        Assert.Single(results);
        Assert.Equal("$.Sender", results[0].XPath);
        Assert.Equal("MAERSK", results[0].Preview);
    }

    [Fact]
    public void ExecuteJsonPath_ExampleJson_FindsShippingInstructionsStatus()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var results = JsonService.ExecuteJsonPath(json, "$.JsonContent.data.shippingInstructionsStatus");

        Assert.Single(results);
        Assert.Equal("RECEIVED", results[0].Preview);
    }

    [Fact]
    public void ExecuteJsonPath_ExampleJson_WildcardFindsAllTopLevelProperties()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        // $.* returns all direct children of the root object
        var results = JsonService.ExecuteJsonPath(json, "$.*");

        // Root has 3 keys: TargetDeserializationType, Sender, JsonContent
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void ExecuteJsonPath_InvalidExpression_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JsonService.ExecuteJsonPath(RootArrayJson, "![bad"));
    }

    [Fact]
    public void ExecuteJsonPath_SampleJson_NestedProperty_ReturnsCorrectLineNumber()
    {
        var results = JsonService.ExecuteJsonPath(SampleJson, "$.store.name");
        Assert.Single(results);
        Assert.Equal("$.store.name", results[0].XPath);
        Assert.Equal("John", results[0].Preview);
        Assert.Equal(3, results[0].LineNumber);
    }

    [Fact]
    public void ExecuteJsonPath_SampleJson_ArrayElement_ReturnsCorrectLineNumber()
    {
        var results = JsonService.ExecuteJsonPath(SampleJson, "$.store.books[1].title");
        Assert.Single(results);
        Assert.Equal("bar", results[0].Preview);
        Assert.Equal(10, results[0].LineNumber);
    }

    [Fact]
    public void ExecuteJsonPath_SampleJson_ObjectToken_PreviewIsEllipsis()
    {
        var results = JsonService.ExecuteJsonPath(SampleJson, "$.store");
        Assert.Single(results);
        Assert.Equal("{\u2026}", results[0].Preview);
    }

    [Fact]
    public void ExecuteJsonPath_SampleJson_ArrayToken_PreviewIsBracketEllipsis()
    {
        var results = JsonService.ExecuteJsonPath(SampleJson, "$.store.books");
        Assert.Single(results);
        Assert.Equal("[\u2026]", results[0].Preview);
    }

    [Fact]
    public void ExecuteJsonPath_SampleJson_NumberValue_PreviewIsPlainText()
    {
        var results = JsonService.ExecuteJsonPath(SampleJson, "$.store.age");
        Assert.Single(results);
        Assert.Equal("30", results[0].Preview);
        Assert.Equal(4, results[0].LineNumber);
    }

    // ── GetAllPaths ────────────────────────────────────────────────────────

    [Fact]
    public void GetAllPaths_ExampleJson_ContainsExpectedTopLevelPaths()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var results = JsonService.GetAllPaths(json);
        var paths = results.Select(r => r.XPath).ToList();

        Assert.Contains("$", paths);
        Assert.Contains("$.TargetDeserializationType", paths);
        Assert.Contains("$.Sender", paths);
        Assert.Contains("$.JsonContent", paths);
    }

    [Fact]
    public void GetAllPaths_ExampleJson_SenderPreviewIsCorrect()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var results = JsonService.GetAllPaths(json);
        var sender = results.First(r => r.XPath == "$.Sender");
        Assert.Equal("MAERSK", sender.Preview);
    }

    [Fact]
    public void GetAllPaths_ExampleJson_NestedPathsArePresent()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        var results = JsonService.GetAllPaths(json);
        var paths = results.Select(r => r.XPath).ToList();

        Assert.Contains("$.JsonContent.specversion", paths);
        Assert.Contains("$.JsonContent.data.shippingInstructionsStatus", paths);
    }

    [Fact]
    public void GetAllPaths_InvalidJson_ReturnsEmptyList()
    {
        var results = JsonService.GetAllPaths("{invalid}");
        Assert.Empty(results);
    }

    // ── FormatJson – syntax error handling ───────────────────────────────
    //
    // incorrectSyntax.json has a trailing comma with no value inside "feedbacks":
    //   "feedbacks":
    // }  ← value missing → JsonReaderException at parse time
    //
    // FormatJson must propagate this exception so the caller (the UI) can
    // display a user-visible error popup instead of silently failing.

    [Fact]
    public void FormatJson_IncorrectSyntaxJson_ThrowsJsonReaderException()
    {
        var json = File.ReadAllText(TestFilePath("incorrectSyntax.json"));
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonService.FormatJson(json));
    }

    [Fact]
    public void FormatJson_IncorrectSyntaxJson_ExceptionMessageDescribesProblem()
    {
        var json = File.ReadAllText(TestFilePath("incorrectSyntax.json"));
        var ex = Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonService.FormatJson(json));
        // The message must be non-empty so the UI can display it
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    // ──────────────────────────── Minify ────────────────────────────

    [Fact]
    public void MinifyJson_FormattedJson_RemovesWhitespace()
    {
        string formatted = JsonService.FormatJson(SampleJson);
        string minified = JsonService.MinifyJson(formatted);

        Assert.DoesNotContain("\n", minified);
        Assert.True(minified.Length < formatted.Length);
    }

    [Fact]
    public void MinifyJson_FormattedJson_ProducesValidJson()
    {
        string minified = JsonService.MinifyJson(SampleJson);
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(minified);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void MinifyJson_FormattedJson_PreservesContent()
    {
        string minified = JsonService.MinifyJson(SampleJson);
        Assert.Contains("John", minified);
        Assert.Contains("30", minified);
    }

    [Fact]
    public void MinifyJson_MinifiedJson_IsIdempotent()
    {
        string minified1 = JsonService.MinifyJson(SampleJson);
        string minified2 = JsonService.MinifyJson(minified1);
        Assert.Equal(minified1, minified2);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static string TestFilePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", filename);
}
