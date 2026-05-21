using System.IO;
using System.Linq;
using System.Xml;
using PathFinder.Services;

namespace PathFinder.Tests;

public class XmlServiceTests
{
    private const string SampleXml =
        "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n" +
        "<xml id=\"789abc\">\n" +
        "    <car>\n" +
        "        <wheel>17789</wheel>\n" +
        "        <color>blue</color>\n" +
        "    </car>\n" +
        "</xml>";

    // Line numbers (1-based):
    //  1: <?xml version="1.0" encoding="utf-16"?>
    //  2: <xml id="789abc">
    //  3:     <car>
    //  4:         <wheel>17789</wheel>
    //  5:         <color>blue</color>
    //  6:     </car>
    //  7: </xml>

    [Fact]
    public void GetXPathAtLine_ColorElement_ReturnsCorrectPath()
    {
        var result = XmlService.GetXPathAtLine(SampleXml, 5);
        Assert.Equal("/xml/car/color", result);
    }

    [Fact]
    public void GetXPathAtLine_WheelElement_ReturnsCorrectPath()
    {
        var result = XmlService.GetXPathAtLine(SampleXml, 4);
        Assert.Equal("/xml/car/wheel", result);
    }

    [Fact]
    public void GetXPathAtLine_CarElement_ReturnsCorrectPath()
    {
        var result = XmlService.GetXPathAtLine(SampleXml, 3);
        Assert.Equal("/xml/car", result);
    }

    [Fact]
    public void GetXPathAtLine_RootElement_ReturnsCorrectPath()
    {
        var result = XmlService.GetXPathAtLine(SampleXml, 2);
        Assert.Equal("/xml", result);
    }

    [Fact]
    public void GetXPathAtLine_SiblingElements_IncludeIndex()
    {
        const string xml =
            "<root>\n" +
            "    <item>a</item>\n" +
            "    <item>b</item>\n" +
            "    <item>c</item>\n" +
            "</root>";

        // Line 3 → second <item>
        var result = XmlService.GetXPathAtLine(xml, 3);
        Assert.Equal("/root/item[2]", result);
    }

    // ── Attribute ──────────────────────────────────────────────────────────

    // SampleXml line 2: <xml id="789abc">
    // Characters:        1234567890123456
    //  col 1='<', 2='x', 3='m', 4='l', 5=' ', 6='i'  → attribute "id" name starts at col 6
    //  col 9='"' (opening), 10='7'…15='c', 16='"' (closing quote of value)
    //  col 17='>'  ← outside attribute span
    [Theory]
    [InlineData(6)]   // on the 'i' of "id"
    [InlineData(7)]   // on the 'd' of "id"
    [InlineData(9)]   // on the '"' opening the value
    [InlineData(12)]  // inside the value "789abc"
    [InlineData(16)]  // on the closing '"' of the value
    public void GetXPathAtLine_AttributeOnLine_ReturnsAttributePath(int col)
    {
        var result = XmlService.GetXPathAtLine(SampleXml, targetLine: 2, targetColumn: col);
        Assert.Equal("/xml/@id", result);
    }

    [Fact]
    public void GetXPathAtLine_NoColumnProvided_ReturnsElementPath()
    {
        // Column 0 means "no column info" — should return element, not attribute
        var result = XmlService.GetXPathAtLine(SampleXml, targetLine: 2, targetColumn: 0);
        Assert.Equal("/xml", result);
    }

    [Fact]
    public void GetXPathAtLine_ColumnBeforeAttribute_ReturnsElementPath()
    {
        // Column 1 is '<', before the 'id' attribute at col 6
        var result = XmlService.GetXPathAtLine(SampleXml, targetLine: 2, targetColumn: 1);
        Assert.Equal("/xml", result);
    }

    [Fact]
    public void GetXPathAtLine_ColumnAfterAttributeClosingQuote_ReturnsElementPath()
    {
        // Col 17 is '>' — past the closing '"' of id="789abc" at col 16
        // Cursor is no longer inside any attribute span → element path expected
        var result = XmlService.GetXPathAtLine(SampleXml, targetLine: 2, targetColumn: 17);
        Assert.Equal("/xml", result);
    }

    // ── Attribute vs. text content on the same line ───────────────────────
    //
    // A single-line element can have BOTH an attribute AND text content:
    //   <tag attr="val">text</tag>
    // The cursor position determines whether we return the attribute path or element path.
    //
    // xml: <root><item key="abc">hello</item></root>
    //       123456789012345678901234567890
    // Line 1: <root>
    // Line 2: <item key="abc">hello</item>   (indented: 0 spaces, just the element)
    //          col: 1234567890123456...
    //   <  = 1
    //   i  = 2, t=3, e=4, m=5
    //   ' '= 6
    //   k  = 7 ← attribute "key" starts here
    //   e  = 8, y=9 → "key" ends col 9
    //   =  = 10
    //   "  = 11 (opening)
    //   a  = 12, b=13, c=14
    //   "  = 15 ← closing quote of "abc"
    //   >  = 16
    //   h  = 17 ← text content starts here
    //   e  = 18, l=19, l=20, o=21
    //   <  = 22 (closing tag)

    private const string InlineAttrTextXml =
        "<root>\n" +
        "<item key=\"abc\">hello</item>\n" +
        "</root>";

    [Theory]
    [InlineData(7)]   // on 'k' — attribute name start
    [InlineData(9)]   // on 'y' — attribute name end
    [InlineData(11)]  // on opening '"'
    [InlineData(13)]  // inside value "abc"
    [InlineData(15)]  // on closing '"' of value
    public void GetXPathAtLine_InlineElement_CursorOnAttribute_ReturnsAttributePath(int col)
    {
        var result = XmlService.GetXPathAtLine(InlineAttrTextXml, targetLine: 2, targetColumn: col);
        Assert.Equal("/root/item/@key", result);
    }

    [Theory]
    [InlineData(16)]  // on '>' — just past attribute span, before text
    [InlineData(17)]  // on 'h' — first char of text content
    [InlineData(19)]  // on 'l' — middle of text content
    [InlineData(21)]  // on 'o' — last char of text content
    public void GetXPathAtLine_InlineElement_CursorOnTextContent_ReturnsElementPath(int col)
    {
        var result = XmlService.GetXPathAtLine(InlineAttrTextXml, targetLine: 2, targetColumn: col);
        Assert.Equal("/root/item", result);
    }

    // ── Format / Indentation ──────────────────────────────────────────────

    // BadFormat.xml has misaligned indentation; FormatXml should normalise to 4-space indent.
    [Fact]
    public void FormatXml_BadFormatXml_ProducesProper4SpaceIndentation()
    {
        var xml = File.ReadAllText(TestFilePath("BadFormat.xml"));
        var result = XmlService.FormatXml(xml);
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // <car> is depth-1 child of root → 4-space indent
        Assert.Contains(lines, l => l == "    <car>");
        // <wheel>/<color> are depth-2 → 8-space indent
        Assert.Contains(lines, l => l == "        <wheel>17</wheel>");
        Assert.Contains(lines, l => l == "        <color>blue</color>");
    }

    // ── File-type support ─────────────────────────────────────────────────

    // All XML-based formats (xml, xsd, xsl) must parse and format without throwing.

    [Fact]
    public void FormatXml_XmlFile_ParsesAndFormatsWithoutError()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var result = XmlService.FormatXml(xml);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void FormatXml_XsdFile_ParsesAndFormatsWithoutError()
    {
        var xml = File.ReadAllText(TestFilePath("example.xsd"));
        var result = XmlService.FormatXml(xml);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("xs:schema", result);
    }

    [Fact]
    public void FormatXml_XslFile_ParsesAndFormatsWithoutError()
    {
        var xml = File.ReadAllText(TestFilePath("example.xsl"));
        var result = XmlService.FormatXml(xml);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("xsl:stylesheet", result);
    }

    // ── XPath highlight / sibling indexing (XPathHighlight.xml) ──────────
    //
    // XPathHighlight.xml line map:
    //  2:  <xml id="789abc">
    //  3:      <car>
    //  4:          <wheel>17789</wheel>
    //  5:          <color>blue</color>
    //  6–53:   (blank lines)
    //  54:         <color>red</color>
    //  55:     </car>
    //  56: </xml>

    [Fact]
    public void GetXPathAtLine_XPathHighlightXml_FirstColorSibling_ReturnsIndexedPath()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var result = XmlService.GetXPathAtLine(xml, 5);
        Assert.Equal("/xml/car/color[1]", result);
    }

    [Fact]
    public void GetXPathAtLine_XPathHighlightXml_SecondColorSibling_ReturnsIndexedPath()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var result = XmlService.GetXPathAtLine(xml, 54);
        Assert.Equal("/xml/car/color[2]", result);
    }

    [Fact]
    public void ExecuteXPath_XPathHighlightXml_ColorExpression_ReturnsTwoResultsWithCorrectLines()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var results = XmlService.ExecuteXPath(xml, "/xml/car/color");

        Assert.Equal(2, results.Count);
        Assert.Equal("/xml/car/color[1]", results[0].XPath);
        Assert.Equal(5, results[0].LineNumber);
        Assert.Equal("/xml/car/color[2]", results[1].XPath);
        Assert.Equal(54, results[1].LineNumber);
    }

    [Fact]
    public void GetAllPaths_XPathHighlightXml_ContainsAllExpectedPaths()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var results = XmlService.GetAllPaths(xml);
        var paths = results.Select(r => r.XPath).ToList();

        Assert.Contains("/xml", paths);
        Assert.Contains("/xml/@id", paths);
        Assert.Contains("/xml/car", paths);
        Assert.Contains("/xml/car/wheel", paths);
        Assert.Contains("/xml/car/color[1]", paths);
        Assert.Contains("/xml/car/color[2]", paths);
    }

    [Fact]
    public void GetAllPaths_XPathHighlightXml_WheelPreviewValue()
    {
        var xml = File.ReadAllText(TestFilePath("XPathHighlight.xml"));
        var results = XmlService.GetAllPaths(xml);
        var wheel = results.First(r => r.XPath == "/xml/car/wheel");
        Assert.Equal("17789", wheel.Preview);
    }

    // ── ExecuteXPath – invalid expression ────────────────────────────────

    [Fact]
    public void ExecuteXPath_InvalidExpression_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            XmlService.ExecuteXPath(SampleXml, "///invalid"));
    }

    // ── GetXPathAtLine – invalid XML ──────────────────────────────────────

    [Fact]
    public void GetXPathAtLine_InvalidXml_ReturnsNull()
    {
        var result = XmlService.GetXPathAtLine("<unclosed>", 1);
        Assert.Null(result);
    }

    // ── Namespace XML ─────────────────────────────────────────────────────
    //
    // namespace.xml has a root element with xmlns:ns0 declared.
    // XPath expressions using the ns0: prefix must work correctly.

    [Fact]
    public void ExecuteXPath_NamespaceXml_NamespacedPathReturnsCorrectValue()
    {
        var xml = File.ReadAllText(TestFilePath("namespace.xml"));
        var results = XmlService.ExecuteXPath(xml, "/ns0:EFACT_D99B_IFTMBF/ns0:CTA/ns0:C056/C05602");

        Assert.Single(results);
        Assert.Equal("CargoWise One Support", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_NamespaceXml_NamespacedPathReturnsCorrectXPath()
    {
        var xml = File.ReadAllText(TestFilePath("namespace.xml"));
        var results = XmlService.ExecuteXPath(xml, "/ns0:EFACT_D99B_IFTMBF/ns0:CTA/ns0:C056/C05602");

        Assert.Single(results);
        Assert.Equal("/ns0:EFACT_D99B_IFTMBF/ns0:CTA/ns0:C056/C05602", results[0].XPath);
    }

    // ── Multiple default namespaces (multipleNamespaces.xml) ─────────────
    //
    // multipleNamespaces.xml uses TWO default namespace declarations (no prefix):
    //   line 2:  <UniversalInterchange xmlns="http://www.cargowise.com/Schemas/Universal/2011/11" ...>
    //   line 8:  <UniversalShipment   xmlns="http://www.cargowise.com/Schemas/Universal/2012/11/..." ...>
    //
    // Without special handling, plain unprefixed XPath returns 0 results because
    // the XPath engine sees the elements as namespace-qualified but the expression
    // has no matching prefix.  ExecuteXPath strips bare xmlns="..." declarations
    // when the expression itself carries no namespace prefixes.
    //
    // Line map (1-based):
    //  2:  <UniversalInterchange ...>
    //  3:  <Header>
    //  4:  <SenderID>HYEDCNUAT</SenderID>
    //  8:  <UniversalShipment ...>
    //  9:  <Shipment>
    //  10: <DataContext>
    //  11: <DataSource>
    //  12: <DataProvider Type="EnterpriseID">HYEUATDCN</DataProvider>

    [Fact]
    public void ExecuteXPath_MultipleDefaultNamespacesXml_RootElementReturnsResult()
    {
        var xml = File.ReadAllText(TestFilePath("multipleNamespaces.xml"));
        var results = XmlService.ExecuteXPath(xml, "/UniversalInterchange");

        Assert.Single(results);
    }

    [Fact]
    public void ExecuteXPath_MultipleDefaultNamespacesXml_FirstNamespaceLeafReturnsCorrectValue()
    {
        // SenderID lives in the first (root) default namespace
        var xml = File.ReadAllText(TestFilePath("multipleNamespaces.xml"));
        var results = XmlService.ExecuteXPath(xml, "/UniversalInterchange/Header/SenderID");

        Assert.Single(results);
        Assert.Equal("HYEDCNUAT", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_MultipleDefaultNamespacesXml_SecondNamespaceLeafReturnsCorrectValue()
    {
        // DataProvider lives inside UniversalShipment, which re-declares a different default namespace
        var xml = File.ReadAllText(TestFilePath("multipleNamespaces.xml"));
        var results = XmlService.ExecuteXPath(xml,
            "/UniversalInterchange/Body/UniversalShipment/Shipment/DataContext/DataSource/DataProvider");

        Assert.Single(results);
        Assert.Equal("HYEUATDCN", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_MultipleDefaultNamespacesXml_DataSourceParentReturnsResult()
    {
        // DataSource has child elements; Preview should be the ellipsis placeholder
        var xml = File.ReadAllText(TestFilePath("multipleNamespaces.xml"));
        var results = XmlService.ExecuteXPath(xml,
            "/UniversalInterchange/Body/UniversalShipment/Shipment/DataContext/DataSource");

        Assert.Single(results);
        Assert.Equal("{…}", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_MultipleDefaultNamespacesXml_DataProviderLineNumber()
    {
        var xml = File.ReadAllText(TestFilePath("multipleNamespaces.xml"));
        var results = XmlService.ExecuteXPath(xml,
            "/UniversalInterchange/Body/UniversalShipment/Shipment/DataContext/DataSource/DataProvider");

        Assert.Single(results);
        Assert.Equal(12, results[0].LineNumber);
    }

    // ── ExecuteXPath – Preview formatting ────────────────────────────────
    //
    // BuildPreview produces different output depending on the matched node type:
    //  • Leaf element (single text child) → plain inner text (≤80 chars, truncates with …)
    //  • Empty element (no children)      → "<TagName />"
    //  • Parent element (child elements)  → "{…}"

    [Fact]
    public void ExecuteXPath_LeafElement_PreviewIsPlainText()
    {
        const string xml = "<root><leaf>hello world</leaf></root>";
        var results = XmlService.ExecuteXPath(xml, "/root/leaf");

        Assert.Single(results);
        Assert.Equal("hello world", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_EmptyElement_PreviewIsSelfClosingTag()
    {
        const string xml = "<root><empty /></root>";
        var results = XmlService.ExecuteXPath(xml, "/root/empty");

        Assert.Single(results);
        Assert.Equal("<empty />", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_ParentElement_PreviewIsEllipsis()
    {
        const string xml = "<root><parent><child>x</child></parent></root>";
        var results = XmlService.ExecuteXPath(xml, "/root/parent");

        Assert.Single(results);
        Assert.Equal("{…}", results[0].Preview);
    }

    [Fact]
    public void ExecuteXPath_LeafElementLongText_PreviewIsTruncatedAt80Chars()
    {
        var longValue = new string('a', 90);
        var xml = $"<root><leaf>{longValue}</leaf></root>";
        var results = XmlService.ExecuteXPath(xml, "/root/leaf");

        Assert.Single(results);
        Assert.Equal(new string('a', 80) + "…", results[0].Preview);
    }

    // ── FormatXml – syntax error handling ───────────────────────────────
    //
    // incorrectSyntax.xml (UTF-16 LE BOM) has an unclosed <car> element:
    //   <xml>
    //       <car>          ← never closed
    //   <wheel>…</wheel>
    //   </xml>             ← XmlException: unexpected end of element
    //
    // FormatXml must propagate this exception so the caller (the UI) can
    // display a user-visible error popup instead of silently failing.

    [Fact]
    public void FormatXml_IncorrectSyntaxXml_ThrowsXmlException()
    {
        var xml = File.ReadAllText(TestFilePath("incorrectSyntax.xml"));
        Assert.Throws<System.Xml.XmlException>(() => XmlService.FormatXml(xml));
    }

    [Fact]
    public void FormatXml_IncorrectSyntaxXml_ExceptionMessageDescribesProblem()
    {
        var xml = File.ReadAllText(TestFilePath("incorrectSyntax.xml"));
        var ex = Assert.Throws<System.Xml.XmlException>(() => XmlService.FormatXml(xml));
        // The message must be non-empty so the UI can display it
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    // ── GenerateSampleXml ─────────────────────────────────────────────────

    // The AcknowledgementMessage.xsd defines a schema with:
    //   - targetNamespace "http://cargowise.com/ehub/products/ocm/gtnexus/2015/03"
    //   - elementFormDefault="unqualified" (child elements are not namespace-qualified)
    //   - Root element AcknowledgementMessage with AcknowledgedType attribute (xs:string)
    //   - TransactionInfo > MessageSender, MessageRecipient, MessageID (xs:string),
    //                       Created (xs:dateTime), FileName (xs:string)
    //   - AcknowledgementGroup > MessageID (xs:unsignedByte), FileName (xs:string)
    //       > Acknowledgement (ShipmentId/Sequence/GtnId attrs)
    //           > Detail (simpleContent with Severity/AuditID attrs)
    //           > References > Reference (simpleContent with referenceType attr)
    //
    // AcknowledgementMessage.xml is the expected sample output.

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_MatchesExpectedXml()
    {
        string xsdPath = TestFilePath("AcknowledgementMessage.xsd");
        string xsdContent = File.ReadAllText(xsdPath, System.Text.Encoding.Unicode);
        string expectedXml = File.ReadAllText(TestFilePath("AcknowledgementMessage.xml"));

        string generated = XmlService.GenerateSampleXml(xsdContent, "AcknowledgementMessage.xsd");

        AssertXmlSemanticEqual(expectedXml, generated);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_RootElementHasCorrectNamespace()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.Equal("http://cargowise.com/ehub/products/ocm/gtnexus/2015/03",
            doc.DocumentElement!.NamespaceURI);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_RootElementNameIsCorrect()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.Equal("AcknowledgementMessage", doc.DocumentElement!.LocalName);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_StringAttributeValueIsString()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.Equal("String", doc.DocumentElement!.GetAttribute("AcknowledgedType"));
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_DateTimeElementValueIsCorrect()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        // Child elements are unqualified (elementFormDefault=unqualified)
        var created = doc.SelectSingleNode("//Created");
        Assert.NotNull(created);
        Assert.Equal("2001-12-17T09:30:47Z", created!.InnerText);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_UnsignedByteAttributeIs255()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        var detail = doc.SelectSingleNode("//Detail");
        Assert.NotNull(detail);
        Assert.Equal("255", ((XmlElement)detail!).GetAttribute("Severity"));
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_UnsignedIntAttributeIs0()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        var detail = doc.SelectSingleNode("//Detail");
        Assert.NotNull(detail);
        Assert.Equal("0", ((XmlElement)detail!).GetAttribute("AuditID"));
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_SchemaLocationPresentWhenFilenameProvided()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent, "AcknowledgementMessage.xsd");

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        const string xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        string schemaLocation = doc.DocumentElement!.GetAttribute("schemaLocation", xsiNs);
        Assert.Contains("AcknowledgementMessage.xsd", schemaLocation);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_SchemaLocationAbsentWhenNoFilenameProvided()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        const string xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        string schemaLocation = doc.DocumentElement!.GetAttribute("schemaLocation", xsiNs);
        Assert.True(string.IsNullOrEmpty(schemaLocation));
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_SimpleContentTextAndAttributesBothPresent()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        // Detail is a simpleContent element — must have both text and attributes
        var detail = doc.SelectSingleNode("//Detail") as XmlElement;
        Assert.NotNull(detail);
        Assert.Equal("String", detail!.InnerText);
        Assert.Equal("255", detail.GetAttribute("Severity"));
        Assert.Equal("0", detail.GetAttribute("AuditID"));
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_MaxOccursUnboundedGeneratesOneInstance()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        // Reference has maxOccurs="unbounded" — should generate exactly 1 sample
        var references = doc.SelectNodes("//Reference");
        Assert.NotNull(references);
        Assert.Equal(1, references!.Count);
    }

    [Fact]
    public void GenerateSampleXml_AcknowledgementXsd_ProducesValidXml()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"),
            System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        // Must be parseable without exception
        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.NotNull(doc.DocumentElement);
    }

    [Fact]
    public void GenerateSampleXml_EmptyXsd_ThrowsInvalidOperationException()
    {
        // An XSD without any global element should throw
        const string xsd = """
            <?xml version="1.0"?>
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
            </xs:schema>
            """;
        Assert.Throws<InvalidOperationException>(() => XmlService.GenerateSampleXml(xsd));
    }

    // ── GenerateSampleXml – CarrierUniversalShipment ──────────────────────
    //
    // CarrierUniversalShipment.xsd is a large, production-style schema that
    // exercises several patterns not present in AcknowledgementMessage.xsd:
    //
    //   • elementFormDefault="qualified"  (all child elements are namespace-qualified)
    //   • Named complex types referenced via type="TypeName" attributes
    //   • Self-referential cycle: Shipment → SubShipmentCollection → SubShipment (type="Shipment")
    //   • xs:all particles (not xs:sequence)
    //   • xs:simpleType restrictions on strings (string_maxLength50, etc.)
    //   • xs:union member types (emptiable_dateTime = xs:dateTime | empty_string)
    //   • simpleContent extensions (CodeDescriptionPair1Char, ContextType)
    //   • minOccurs="0" (optional elements — included in the sample output)

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_ProducesValidXml()
    {
        // Regression test: before the cycle-detection fix, this XSD caused a
        // StackOverflowException because SubShipment has type="Shipment".
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        // Must be parseable without exception
        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.NotNull(doc.DocumentElement);
    }

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_RootElementIsUniversalShipment()
    {
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.Equal("UniversalShipment", doc.DocumentElement!.LocalName);
    }

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_RootHasTargetNamespace()
    {
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        Assert.Equal("http://www.cargowise.com/Schemas/Universal/2011/11",
            doc.DocumentElement!.NamespaceURI);
    }

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_ShipmentChildPresent()
    {
        // elementFormDefault="qualified" — Shipment child should exist and be namespace-qualified
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        const string ns = "http://www.cargowise.com/Schemas/Universal/2011/11";
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("u", ns);
        var shipment = doc.SelectSingleNode("//u:Shipment", nsMgr);
        Assert.NotNull(shipment);
    }

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_SubShipmentDoesNotRecurseInfinitely()
    {
        // SubShipment has type="Shipment".
        // count 0 → first visit (root Shipment): write with all children
        // count 1 → second visit (SubShipment): write with all children again
        // count ≥2 → skip entirely
        // So SubShipment IS generated (count 1 < 2), but any SubShipment inside its
        // nested SubShipmentCollection is skipped (would be count 2).
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        // No StackOverflowException — cycle was broken.
        Assert.NotNull(doc.DocumentElement);

        const string ns = "http://www.cargowise.com/Schemas/Universal/2011/11";
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("u", ns);

        // SubShipment IS written (2nd visit, count 1).
        var subShipment = doc.SelectSingleNode("//u:SubShipmentCollection/u:SubShipment", nsMgr);
        Assert.NotNull(subShipment);

        // A third-level SubShipment inside the nested SubShipmentCollection is skipped (count 2).
        var deepSubShipment = subShipment!.SelectSingleNode("u:SubShipmentCollection/u:SubShipment", nsMgr);
        Assert.Null(deepSubShipment);
    }

    [Fact]
    public void GenerateSampleXml_CarrierUniversalShipmentXsd_VersionAttributePresent()
    {
        // UniversalShipmentData has xs:attribute name="version" type="xs:token"
        string xsdContent = File.ReadAllText(
            TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);

        string generated = XmlService.GenerateSampleXml(xsdContent);

        var doc = new XmlDocument();
        doc.LoadXml(generated);
        // The root element's type (UniversalShipmentData) has a version attribute
        string version = doc.DocumentElement!.GetAttribute("version");
        Assert.Equal("String", version);
    }

    // Compares two XML documents semantically: element names, namespace URIs,
    // non-namespace attributes (name + value, order-independent), and leaf text.
    private static void AssertXmlSemanticEqual(string expectedXml, string actualXml)
    {
        var expectedDoc = new XmlDocument { XmlResolver = null };
        expectedDoc.LoadXml(expectedXml);

        var actualDoc = new XmlDocument { XmlResolver = null };
        actualDoc.LoadXml(actualXml);

        Assert.NotNull(expectedDoc.DocumentElement);
        Assert.NotNull(actualDoc.DocumentElement);

        AssertElementEqual(expectedDoc.DocumentElement!, actualDoc.DocumentElement!);
    }

    private static void AssertElementEqual(XmlElement expected, XmlElement actual)
    {
        Assert.Equal(expected.LocalName, actual.LocalName);
        Assert.Equal(expected.NamespaceURI, actual.NamespaceURI);

        // Compare non-namespace-declaration attributes (order-independent)
        var xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        var xmlnsNs = "http://www.w3.org/2000/xmlns/";
        var expectedAttrs = expected.Attributes.Cast<XmlAttribute>()
            .Where(a => a.NamespaceURI != xsiNs && a.NamespaceURI != xmlnsNs)
            .OrderBy(a => a.LocalName)
            .ToList();
        var actualAttrs = actual.Attributes.Cast<XmlAttribute>()
            .Where(a => a.NamespaceURI != xsiNs && a.NamespaceURI != xmlnsNs)
            .OrderBy(a => a.LocalName)
            .ToList();

        Assert.Equal(expectedAttrs.Count, actualAttrs.Count);
        for (int i = 0; i < expectedAttrs.Count; i++)
        {
            Assert.Equal(expectedAttrs[i].LocalName, actualAttrs[i].LocalName);
            Assert.Equal(expectedAttrs[i].Value, actualAttrs[i].Value);
        }

        // Compare child elements recursively
        var expectedChildren = expected.ChildNodes.OfType<XmlElement>().ToList();
        var actualChildren = actual.ChildNodes.OfType<XmlElement>().ToList();

        Assert.Equal(expectedChildren.Count, actualChildren.Count);
        for (int i = 0; i < expectedChildren.Count; i++)
            AssertElementEqual(expectedChildren[i], actualChildren[i]);

        // Compare leaf text content
        if (!expectedChildren.Any())
            Assert.Equal(expected.InnerText.Trim(), actual.InnerText.Trim());
    }

    // ── GenerateSampleXml — complexContent extension (PortOrder XSD) ────────

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_ProducesValidXml()
    {
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(generated); // throws if not valid XML
        Assert.NotNull(doc.DocumentElement);
    }

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_RootElementIsPortOrder()
    {
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(generated);
        Assert.Equal("PortOrder", doc.DocumentElement!.LocalName);
    }

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_HasTransactionChild()
    {
        // PortOrderElementType extends ExchangeType (which provides Transaction)
        // via xs:complexContent/xs:extension — the base type's particle must be emitted.
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(generated);
        var transaction = doc.DocumentElement!.SelectSingleNode("Transaction");
        Assert.NotNull(transaction);
    }

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_HasMessageChild()
    {
        // Message is declared in the xs:extension body of PortOrderElementType.
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(generated);
        var message = doc.DocumentElement!.SelectSingleNode("Message");
        Assert.NotNull(message);
    }

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_TransactionHasChildElements()
    {
        // TransactionType is a plain sequence — its children must be generated correctly.
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(generated);
        var transaction = doc.DocumentElement!.SelectSingleNode("Transaction") as XmlElement;
        Assert.NotNull(transaction);
        Assert.True(transaction!.ChildNodes.OfType<XmlElement>().Any(),
            "Transaction must have child elements (IOPartner, IOReference, etc.)");
    }

    [Fact]
    public void GenerateSampleXml_PortOrderXsd_ValidatesAgainstItsOwnSchema()
    {
        string xsdContent = File.ReadAllText(TestFilePath("PortOrder-QUAY.xsd"));
        string generated = XmlService.GenerateSampleXml(xsdContent);
        var errors = XmlService.ValidateXmlAgainstXsd(generated, xsdContent);
        Assert.Empty(errors);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static string TestFilePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", filename);

    // ── ValidateXmlAgainstXsd tests ────────────────────────────────────────

    [Fact]
    public void ValidateXmlAgainstXsd_ValidXml_ReturnsEmptyList()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"), System.Text.Encoding.Unicode);
        string xmlContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xml"));

        var errors = XmlService.ValidateXmlAgainstXsd(xmlContent, xsdContent);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateXmlAgainstXsd_InvalidXml_ReturnsErrors()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"), System.Text.Encoding.Unicode);
        // XML root matches the schema namespace but has an unexpected child element
        const string badXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <n1:AcknowledgementMessage
                xmlns:n1="http://cargowise.com/ehub/products/ocm/gtnexus/2015/03"
                AcknowledgedType="String">
              <UnknownElement>value</UnknownElement>
            </n1:AcknowledgementMessage>
            """;

        var errors = XmlService.ValidateXmlAgainstXsd(badXml, xsdContent);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateXmlAgainstXsd_InvalidXml_ErrorContainsLineNumber()
    {
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"), System.Text.Encoding.Unicode);
        const string badXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <n1:AcknowledgementMessage
                xmlns:n1="http://cargowise.com/ehub/products/ocm/gtnexus/2015/03"
                AcknowledgedType="String">
              <UnknownElement>value</UnknownElement>
            </n1:AcknowledgementMessage>
            """;

        var errors = XmlService.ValidateXmlAgainstXsd(badXml, xsdContent);

        Assert.Contains(errors, e => e.StartsWith("Line ", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateXmlAgainstXsd_MalformedXsd_ThrowsException()
    {
        const string badXsd = "<this is not valid xsd>";
        const string xml = "<root/>";

        Assert.ThrowsAny<Exception>(() => XmlService.ValidateXmlAgainstXsd(xml, badXsd));
    }

    [Fact]
    public void ValidateXmlAgainstXsd_AcknowledgementXsd_ValidSampleXml_ReturnsEmptyList()
    {
        // AcknowledgementMessage.xsd has no self-referential cycles, so its
        // generated sample is structurally complete and must be valid against the XSD.
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"), System.Text.Encoding.Unicode);
        string sampleXml = XmlService.GenerateSampleXml(xsdContent, "AcknowledgementMessage.xsd");

        var errors = XmlService.ValidateXmlAgainstXsd(sampleXml, xsdContent);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateXmlAgainstXsd_CarrierUniversalShipmentXsd_ValidSampleXml_ReturnsEmptyList()
    {
        // CarrierUniversalShipment.xsd has self-referential cycles (SubShipment→Shipment,
        // SubContext→Context). The generator must skip those recursive elements rather than
        // writing empty elements, so the generated sample validates cleanly.
        string xsdContent = File.ReadAllText(TestFilePath("CarrierUniversalShipment.xsd"), System.Text.Encoding.Unicode);
        string sampleXml = XmlService.GenerateSampleXml(xsdContent, "CarrierUniversalShipment.xsd");

        var errors = XmlService.ValidateXmlAgainstXsd(sampleXml, xsdContent);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateXmlAgainstXsd_UnrelatedXml_ReturnsError()
    {
        // XML whose root element / namespace has nothing to do with the XSD must
        // produce an error, not silently pass (lax validation regression).
        string xsdContent = File.ReadAllText(TestFilePath("AcknowledgementMessage.xsd"), System.Text.Encoding.Unicode);
        const string unrelatedXml = "<Invoice xmlns=\"urn:unrelated\"><Number>42</Number></Invoice>";

        var errors = XmlService.ValidateXmlAgainstXsd(unrelatedXml, xsdContent);

        Assert.NotEmpty(errors);
    }

    // ──────────────────────────── Minify ────────────────────────────

    [Fact]
    public void MinifyXml_FormattedXml_RemovesWhitespace()
    {
        string formatted = XmlService.FormatXml(SampleXml);
        string minified = XmlService.MinifyXml(formatted);

        Assert.DoesNotContain("\n", minified);
        Assert.True(minified.Length < formatted.Length);
    }

    [Fact]
    public void MinifyXml_FormattedXml_ProducesValidXml()
    {
        string minified = XmlService.MinifyXml(SampleXml);
        var doc = new XmlDocument();
        doc.LoadXml(minified);
        Assert.NotNull(doc.DocumentElement);
    }

    [Fact]
    public void MinifyXml_FormattedXml_PreservesContent()
    {
        string minified = XmlService.MinifyXml(SampleXml);
        Assert.Contains("17789", minified);
        Assert.Contains("blue", minified);
    }

    [Fact]
    public void MinifyXml_MinifiedXml_IsIdempotent()
    {
        string minified1 = XmlService.MinifyXml(SampleXml);
        string minified2 = XmlService.MinifyXml(minified1);
        Assert.Equal(minified1, minified2);
    }
}
