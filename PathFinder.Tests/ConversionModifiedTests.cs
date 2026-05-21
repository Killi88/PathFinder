using System.IO;
using System.Xml;
using PathFinder.Services;

namespace PathFinder.Tests;

/// <summary>
/// Tests that XML↔JSON conversion and XSD→Sample XML generation produce
/// non-empty, valid output. The MainWindow UI sets IsModified = true on the
/// new tab after these operations; these tests verify the generated content
/// is valid and non-empty (the precondition for the modified indicator and
/// the save prompt on close).
/// </summary>
public class ConversionModifiedTests
{
    private static string TestFilePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", filename);

    // ── XML → JSON ──────────────────────────────────────────────────────

    [Fact]
    public void ConvertXmlToJson_SampleXml_ProducesNonEmptyJson()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        json = JsonService.FormatJson(json);

        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void ConvertXmlToJson_SampleXml_ProducesValidJson()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        json = JsonService.FormatJson(json);

        // Verify it parses as valid JSON
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ConvertXmlToJson_SampleXml_PreservesContentValues()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        json = JsonService.FormatJson(json);

        Assert.Contains("test", json);
        Assert.Contains("42", json);
    }

    [Fact]
    public void ConvertXmlToJson_XPathHighlightFile_ProducesValidJson()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        json = JsonService.FormatJson(json);

        Assert.False(string.IsNullOrWhiteSpace(json));
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(json);
        Assert.NotNull(parsed);
    }

    // ── JSON → XML ──────────────────────────────────────────────────────

    [Fact]
    public void ConvertJsonToXml_SampleJson_ProducesNonEmptyXml()
    {
        var json = "{\"root\":{\"name\":\"test\",\"value\":42}}";
        var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        Assert.False(string.IsNullOrWhiteSpace(xml));
    }

    [Fact]
    public void ConvertJsonToXml_SampleJson_ProducesValidXml()
    {
        var json = "{\"root\":{\"name\":\"test\",\"value\":42}}";
        var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        // Verify it parses as valid XML
        var reparsed = new XmlDocument();
        reparsed.LoadXml(xml);
        Assert.NotNull(reparsed.DocumentElement);
    }

    [Fact]
    public void ConvertJsonToXml_SampleJson_PreservesContentValues()
    {
        var json = "{\"root\":{\"name\":\"test\",\"value\":42}}";
        var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        Assert.Contains("test", xml);
        Assert.Contains("42", xml);
    }

    [Fact]
    public void ConvertJsonToXml_ExampleJsonFile_ProducesValidXml()
    {
        var json = File.ReadAllText(TestFilePath("example.json"));
        // Multi-property JSON root needs a wrapper element (same logic as MainWindow.ConvertFormat)
        XmlDocument? xmlDoc;
        try { xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json); }
        catch { xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode("{\"root\":" + json + "}"); }

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        Assert.False(string.IsNullOrWhiteSpace(xml));
        var reparsed = new XmlDocument();
        reparsed.LoadXml(xml);
        Assert.NotNull(reparsed.DocumentElement);
    }

    // ── Generate Sample XML from XSD ────────────────────────────────────

    [Fact]
    public void GenerateSampleXmlFromXsd_AcknowledgementXsd_ProducesNonEmptyXml()
    {
        var xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"));

        string sampleXml = XmlService.GenerateSampleXml(xsdContent, "AcknowledgementMessage.xsd");
        sampleXml = XmlService.FormatXml(sampleXml);

        Assert.False(string.IsNullOrWhiteSpace(sampleXml));
    }

    [Fact]
    public void GenerateSampleXmlFromXsd_AcknowledgementXsd_ProducesValidXml()
    {
        var xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"));

        string sampleXml = XmlService.GenerateSampleXml(xsdContent, "AcknowledgementMessage.xsd");
        sampleXml = XmlService.FormatXml(sampleXml);

        var doc = new XmlDocument();
        doc.LoadXml(sampleXml);
        Assert.NotNull(doc.DocumentElement);
    }

    [Fact]
    public void GenerateSampleXmlFromXsd_CarrierXsd_ProducesNonEmptyXml()
    {
        var xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string sampleXml = XmlService.GenerateSampleXml(xsdContent);
        sampleXml = XmlService.FormatXml(sampleXml);

        Assert.False(string.IsNullOrWhiteSpace(sampleXml));
    }

    [Fact]
    public void GenerateSampleXmlFromXsd_CarrierXsd_ProducesValidXml()
    {
        var xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string sampleXml = XmlService.GenerateSampleXml(xsdContent);
        sampleXml = XmlService.FormatXml(sampleXml);

        var doc = new XmlDocument();
        doc.LoadXml(sampleXml);
        Assert.NotNull(doc.DocumentElement);
    }

    // ── Round-trip: XML → JSON → XML ────────────────────────────────────

    [Fact]
    public void RoundTrip_XmlToJsonToXml_ProducesValidXml()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        // XML → JSON
        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        json = JsonService.FormatJson(json);

        // JSON → XML
        var xmlDoc2 = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);
        Assert.NotNull(xmlDoc2);
        string xml2 = XmlService.FormatXml(xmlDoc2!.OuterXml);

        // Final output is still valid XML with content
        var reparsed = new XmlDocument();
        reparsed.LoadXml(xml2);
        Assert.Contains("test", xml2);
        Assert.Contains("42", xml2);
    }

    // ── XML → YAML ──────────────────────────────────────────────────────

    [Fact]
    public void ConvertXmlToYaml_SampleXml_ProducesNonEmptyYaml()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        string yaml = YamlService.ConvertJsonToYaml(json);

        Assert.False(string.IsNullOrWhiteSpace(yaml));
    }

    [Fact]
    public void ConvertXmlToYaml_SampleXml_PreservesContentValues()
    {
        var xml = "<root><name>test</name><value>42</value></root>";
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
        string yaml = YamlService.ConvertJsonToYaml(json);

        Assert.Contains("test", yaml);
        Assert.Contains("42", yaml);
    }

    // ── YAML → XML ──────────────────────────────────────────────────────

    [Fact]
    public void ConvertYamlToXml_SampleYaml_ProducesValidXml()
    {
        var yaml = "root:\n  name: test\n  value: 42";
        string json = YamlService.ConvertYamlToJson(yaml);
        var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        Assert.False(string.IsNullOrWhiteSpace(xml));
        var reparsed = new XmlDocument();
        reparsed.LoadXml(xml);
        Assert.NotNull(reparsed.DocumentElement);
    }

    [Fact]
    public void ConvertYamlToXml_SampleYaml_PreservesContentValues()
    {
        var yaml = "root:\n  name: test\n  value: 42";
        string json = YamlService.ConvertYamlToJson(yaml);
        var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json);

        Assert.NotNull(xmlDoc);
        string xml = XmlService.FormatXml(xmlDoc!.OuterXml);

        Assert.Contains("test", xml);
        Assert.Contains("42", xml);
    }

    // ── JSON → YAML ─────────────────────────────────────────────────────

    [Fact]
    public void ConvertJsonToYaml_SampleJson_ProducesNonEmptyYaml()
    {
        var json = "{\"name\":\"test\",\"value\":42}";
        string yaml = YamlService.ConvertJsonToYaml(json);

        Assert.False(string.IsNullOrWhiteSpace(yaml));
    }

    [Fact]
    public void ConvertJsonToYaml_SampleJson_PreservesContentValues()
    {
        var json = "{\"name\":\"test\",\"value\":42}";
        string yaml = YamlService.ConvertJsonToYaml(json);

        Assert.Contains("test", yaml);
        Assert.Contains("42", yaml);
    }

    // ── Sample JSON from Schema ─────────────────────────────────────────

    [Fact]
    public void GenerateSampleJson_SimpleSchema_ProducesNonEmptyJson()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" },
                "active": { "type": "boolean" }
            },
            "required": ["name"]
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.False(string.IsNullOrWhiteSpace(sample));
    }

    [Fact]
    public void GenerateSampleJson_SimpleSchema_ProducesValidJson()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
            }
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(sample);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void GenerateSampleJson_SimpleSchema_ContainsExpectedKeys()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
            }
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.Contains("\"name\"", sample);
        Assert.Contains("\"age\"", sample);
    }

    [Fact]
    public void GenerateSampleJson_WithEnum_UsesFirstEnumValue()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "color": { "type": "string", "enum": ["red", "green", "blue"] }
            }
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.Contains("red", sample);
    }

    [Fact]
    public void GenerateSampleJson_WithArray_ProducesArray()
    {
        var schema = """
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

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.Contains("[", sample);
        Assert.Contains("\"String\"", sample);
    }

    [Fact]
    public void GenerateSampleJson_WithRef_ResolvesReference()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "address": { "$ref": "#/$defs/Address" }
            },
            "$defs": {
                "Address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" }
                    }
                }
            }
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.Contains("\"street\"", sample);
        Assert.Contains("\"city\"", sample);
    }

    [Fact]
    public void GenerateSampleJson_DateTimeFormat_ProducesDateTimeString()
    {
        var schema = """
        {
            "type": "object",
            "properties": {
                "created": { "type": "string", "format": "date-time" }
            }
        }
        """;

        string sample = JsonSchemaService.GenerateSampleJson(schema);
        Assert.Contains("2001-12-17T09:30:47Z", sample);
    }

    [Fact]
    public void GenerateSampleJson_EmptyInput_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => JsonSchemaService.GenerateSampleJson(""));
    }
}
