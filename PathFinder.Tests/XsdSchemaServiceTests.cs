using System.IO;
using PathFinder.Services;

namespace PathFinder.Tests;

public class XsdSchemaServiceTests
{
    private static string ReadTestFile(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestFiles", filename));

    // ──────────────────────────── ParseXsdSchema ────────────────────────────

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_ReturnsNonEmptyList()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.NotEmpty(roots);
    }

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_RootElementIsAcknowledgementMessage()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.Equal("AcknowledgementMessage", roots[0].Name);
    }

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_RootIsComplexType()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.Equal("complex", roots[0].TypeKind);
    }

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_RootHasChildren()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.NotEmpty(roots[0].Children);
    }

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_ContainsAttributes()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var attrs = roots[0].Children.Where(c => c.IsAttribute).ToList();
        Assert.NotEmpty(attrs);
    }

    [Fact]
    public void ParseXsdSchema_AcknowledgementXsd_AttributeHasSimpleTypeKind()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var attr = roots[0].Children.First(c => c.IsAttribute);
        Assert.Equal("simple", attr.TypeKind);
    }

    [Fact]
    public void ParseXsdSchema_CarrierUniversalShipmentXsd_HandlesRecursiveTypes()
    {
        var xsd = ReadTestFile("CarrierUniversalShipment.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.NotEmpty(roots);

        // Should have at least some descendants but not stack overflow
        int countAll = CountAllNodes(roots);
        Assert.True(countAll > 5);
    }

    [Fact]
    public void ParseXsdSchema_CarrierUniversalShipmentXsd_MarksRecursiveNodes()
    {
        var xsd = ReadTestFile("CarrierUniversalShipment.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);

        var recursive = FindNodes(roots, n => n.IsRecursive);
        Assert.NotEmpty(recursive);
    }

    [Fact]
    public void ParseXsdSchema_PortOrderXsd_HandlesComplexContentExtension()
    {
        var xsd = ReadTestFile("PortOrder-QUAY.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.NotEmpty(roots);
        Assert.Equal("PortOrder", roots[0].Name);
    }

    [Fact]
    public void ParseXsdSchema_PortOrderXsd_ExtensionChildrenPresent()
    {
        var xsd = ReadTestFile("PortOrder-QUAY.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);

        var allChildren = roots[0].Children.Where(c => !c.IsAttribute).ToList();
        Assert.NotEmpty(allChildren);
    }

    [Fact]
    public void ParseXsdSchema_ExampleXsd_ParsesWithoutError()
    {
        var xsd = ReadTestFile("example.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.NotEmpty(roots);
    }

    [Fact]
    public void ParseXsdSchema_EmptyInput_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            XsdSchemaService.ParseXsdSchema(""));
    }

    [Fact]
    public void ParseXsdSchema_InvalidXml_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() =>
            XsdSchemaService.ParseXsdSchema("<not-a-schema/>"));
    }

    [Fact]
    public void ParseXsdSchema_NoGlobalElements_ThrowsInvalidOperationException()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:complexType name="SomeType">
                    <xs:sequence>
                        <xs:element name="A" type="xs:string"/>
                    </xs:sequence>
                </xs:complexType>
            </xs:schema>
            """;
        Assert.Throws<InvalidOperationException>(() =>
            XsdSchemaService.ParseXsdSchema(xsd));
    }

    // ──────────────────────────── Choice ────────────────────────────

    [Fact]
    public void ParseXsdSchema_ChoiceGroup_MarksChoiceChildren()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:element name="Root">
                    <xs:complexType>
                        <xs:choice>
                            <xs:element name="OptionA" type="xs:string"/>
                            <xs:element name="OptionB" type="xs:int"/>
                        </xs:choice>
                    </xs:complexType>
                </xs:element>
            </xs:schema>
            """;
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var choices = roots[0].Children.Where(c => c.IsChoice).ToList();
        Assert.Equal(2, choices.Count);
    }

    [Fact]
    public void ParseXsdSchema_ChoiceGroup_ChoiceGroupIdIsSame()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:element name="Root">
                    <xs:complexType>
                        <xs:choice>
                            <xs:element name="OptionA" type="xs:string"/>
                            <xs:element name="OptionB" type="xs:int"/>
                        </xs:choice>
                    </xs:complexType>
                </xs:element>
            </xs:schema>
            """;
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var choices = roots[0].Children.Where(c => c.IsChoice).ToList();
        Assert.True(choices[0].ChoiceGroup.HasValue);
        Assert.Equal(choices[0].ChoiceGroup, choices[1].ChoiceGroup);
    }

    // ──────────────────────────── Restrictions ────────────────────────────

    [Fact]
    public void ParseXsdSchema_Enumeration_ExtractsEnumValues()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:element name="Root">
                    <xs:complexType>
                        <xs:sequence>
                            <xs:element name="Status">
                                <xs:simpleType>
                                    <xs:restriction base="xs:string">
                                        <xs:enumeration value="Active"/>
                                        <xs:enumeration value="Inactive"/>
                                    </xs:restriction>
                                </xs:simpleType>
                            </xs:element>
                        </xs:sequence>
                    </xs:complexType>
                </xs:element>
            </xs:schema>
            """;
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var status = roots[0].Children.First(c => c.Name == "Status");
        Assert.Contains("enumeration", status.Restrictions.Keys);
        Assert.Contains("Active", status.Restrictions["enumeration"]);
    }

    [Fact]
    public void ParseXsdSchema_PatternFacet_ExtractsPattern()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:element name="Root">
                    <xs:complexType>
                        <xs:sequence>
                            <xs:element name="Code">
                                <xs:simpleType>
                                    <xs:restriction base="xs:string">
                                        <xs:pattern value="[A-Z]{3}"/>
                                    </xs:restriction>
                                </xs:simpleType>
                            </xs:element>
                        </xs:sequence>
                    </xs:complexType>
                </xs:element>
            </xs:schema>
            """;
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var code = roots[0].Children.First(c => c.Name == "Code");
        Assert.Contains("pattern", code.Restrictions.Keys);
        Assert.Equal("[A-Z]{3}", code.Restrictions["pattern"]);
    }

    // ──────────────────────────── Documentation ────────────────────────────

    [Fact]
    public void ParseXsdSchema_AnnotatedElement_ExtractsDocumentation()
    {
        string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
                <xs:element name="Root">
                    <xs:annotation>
                        <xs:documentation>Root element docs</xs:documentation>
                    </xs:annotation>
                    <xs:complexType>
                        <xs:sequence>
                            <xs:element name="A" type="xs:string"/>
                        </xs:sequence>
                    </xs:complexType>
                </xs:element>
            </xs:schema>
            """;
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        Assert.Equal("Root element docs", roots[0].Documentation);
    }

    // ──────────────────────────── Statistics ────────────────────────────

    [Fact]
    public void GetStatistics_AcknowledgementXsd_ReturnsNonZeroCounts()
    {
        var xsd = ReadTestFile("AcknowledgementMessage.xsd");
        var roots = XsdSchemaService.ParseXsdSchema(xsd);
        var stats = XsdSchemaService.GetStatistics(roots);
        Assert.True(stats.Elements > 0);
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static int CountAllNodes(List<PathFinder.Models.SchemaNode> nodes)
    {
        int count = nodes.Count;
        foreach (var n in nodes)
            count += CountAllNodes(n.Children);
        return count;
    }

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
