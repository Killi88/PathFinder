using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using PathFinder.Models;

namespace PathFinder.Services;

/// <summary>
/// Parses XSD schemas into <see cref="SchemaNode"/> trees for visualization.
/// Uses System.Xml.Schema compiled schema APIs — same infrastructure as
/// <see cref="XmlService.GenerateSampleXml"/>.
/// </summary>
public static class XsdSchemaService
{
    private const int MaxTraversalDepth = 200;

    private static readonly XmlReaderSettings SafeReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    /// <summary>
    /// Parses an XSD schema and returns one <see cref="SchemaNode"/> per global element.
    /// </summary>
    public static List<SchemaNode> ParseXsdSchema(string xsdContent)
    {
        if (string.IsNullOrWhiteSpace(xsdContent))
            throw new InvalidOperationException("XSD content is empty.");

        XmlSchema schema;
        using (var reader = XmlReader.Create(new StringReader(xsdContent), SafeReaderSettings))
        {
            schema = XmlSchema.Read(reader, null)
                ?? throw new InvalidOperationException("Failed to read XSD schema.");
        }

        var schemaSet = new XmlSchemaSet { XmlResolver = null };
        schemaSet.Add(schema);
        schemaSet.Compile();

        var roots = new List<SchemaNode>();
        foreach (XmlSchemaElement el in schemaSet.GlobalElements.Values)
        {
            var visited = new Dictionary<XmlSchemaComplexType, int>(ReferenceEqualityComparer.Instance);
            roots.Add(BuildElementNode(el, 0, visited, choiceGroupCounter: new Counter()));
        }

        if (roots.Count == 0)
            throw new InvalidOperationException("No global element found in XSD schema.");

        return roots;
    }

    /// <summary>
    /// Returns (totalElements, complexTypes, simpleTypes) from a parsed schema tree.
    /// </summary>
    public static (int Elements, int ComplexTypes, int SimpleTypes) GetStatistics(
        List<SchemaNode> roots)
    {
        int elements = 0, complex = 0, simple = 0;
        CountNodes(roots, ref elements, ref complex, ref simple);
        return (elements, complex, simple);
    }

    // ──────────────────────────── Element Building ────────────────────────────

    private static SchemaNode BuildElementNode(
        XmlSchemaElement element,
        int depth,
        Dictionary<XmlSchemaComplexType, int> visited,
        Counter choiceGroupCounter,
        bool isChoice = false,
        int? choiceGroup = null,
        int? choiceOption = null)
    {
        string name = element.Name ?? element.RefName?.Name ?? "(anonymous)";
        string minOccurs = element.MinOccursString ?? "1";
        string maxOccurs = element.MaxOccursString ?? "1";
        var schemaType = element.ElementSchemaType;

        string typeName = CleanTypeName(element.SchemaTypeName);
        string typeKind = "";
        string? documentation = GetDocumentation(element);
        bool isRecursive = false;
        bool isTruncated = false;

        if (depth > MaxTraversalDepth)
            isTruncated = true;

        var children = new List<SchemaNode>();

        if (!isTruncated && schemaType is XmlSchemaComplexType complexType)
        {
            typeKind = "complex";
            bool isNamedType = !string.IsNullOrEmpty(complexType.Name);
            if (typeName.Length == 0 && isNamedType)
                typeName = complexType.Name!;

            visited.TryGetValue(complexType, out int currentCount);

            if (isNamedType && currentCount >= 1)
            {
                isRecursive = true;
            }
            else
            {
                if (isNamedType)
                    visited[complexType] = currentCount + 1;

                // Add attributes
                foreach (XmlSchemaAttribute attr in complexType.AttributeUses.Values)
                {
                    children.Add(BuildAttributeNode(attr));
                }

                // Add child elements from particle
                CollectParticleChildren(complexType.ContentTypeParticle, depth + 1,
                    visited, children, choiceGroupCounter);

                if (isNamedType)
                {
                    if (currentCount == 0)
                        visited.Remove(complexType);
                    else
                        visited[complexType] = currentCount;
                }
            }
        }
        else if (!isTruncated && schemaType is XmlSchemaSimpleType simpleType)
        {
            typeKind = "simple";
            if (typeName.Length == 0 && !simpleType.QualifiedName.IsEmpty)
                typeName = CleanTypeName(simpleType.QualifiedName);
        }

        var restrictions = ExtractRestrictions(element);

        var node = new SchemaNode
        {
            Name = name,
            TypeName = typeName,
            TypeKind = typeKind,
            MinOccurs = minOccurs,
            MaxOccurs = maxOccurs,
            IsRequired = minOccurs != "0",
            IsChoice = isChoice,
            ChoiceGroup = choiceGroup,
            ChoiceOption = choiceOption,
            IsRecursive = isRecursive,
            IsTruncated = isTruncated,
            Documentation = documentation,
            Restrictions = restrictions,
            IsExpanded = depth <= 1
        };

        foreach (var child in children)
            node.Children.Add(child);

        return node;
    }

    private static SchemaNode BuildAttributeNode(XmlSchemaAttribute attr)
    {
        string name = attr.Name ?? "(anonymous)";
        string typeName = CleanTypeName(attr.SchemaTypeName);
        if (typeName.Length == 0 && attr.AttributeSchemaType is XmlSchemaSimpleType st
            && !st.QualifiedName.IsEmpty)
            typeName = CleanTypeName(st.QualifiedName);

        string minOccurs = attr.Use == XmlSchemaUse.Required ? "1" : "0";

        var restrictions = new Dictionary<string, string>();
        if (attr.AttributeSchemaType is XmlSchemaSimpleType attrSimple)
            ExtractSimpleTypeRestrictions(attrSimple, restrictions);

        return new SchemaNode
        {
            Name = name,
            TypeName = typeName,
            TypeKind = "simple",
            MinOccurs = minOccurs,
            MaxOccurs = "1",
            IsRequired = attr.Use == XmlSchemaUse.Required,
            IsAttribute = true,
            Documentation = GetDocumentation(attr),
            Restrictions = restrictions,
            IsExpanded = false
        };
    }

    // ──────────────────────────── Particle Walking ────────────────────────────

    private static void CollectParticleChildren(
        XmlSchemaParticle? particle,
        int depth,
        Dictionary<XmlSchemaComplexType, int> visited,
        List<SchemaNode> children,
        Counter choiceGroupCounter)
    {
        if (particle is null) return;

        switch (particle)
        {
            case XmlSchemaSequence seq:
                foreach (XmlSchemaObject item in seq.Items)
                    CollectItemChildren(item, depth, visited, children, choiceGroupCounter);
                break;
            case XmlSchemaAll all:
                foreach (XmlSchemaObject item in all.Items)
                    CollectItemChildren(item, depth, visited, children, choiceGroupCounter);
                break;
            case XmlSchemaChoice choice:
                CollectChoiceChildren(choice, depth, visited, children, choiceGroupCounter);
                break;
            case XmlSchemaElement el:
                children.Add(BuildElementNode(el, depth, visited, choiceGroupCounter));
                break;
        }
    }

    private static void CollectItemChildren(
        XmlSchemaObject item,
        int depth,
        Dictionary<XmlSchemaComplexType, int> visited,
        List<SchemaNode> children,
        Counter choiceGroupCounter)
    {
        switch (item)
        {
            case XmlSchemaElement el:
                children.Add(BuildElementNode(el, depth, visited, choiceGroupCounter));
                break;
            case XmlSchemaSequence seq:
                foreach (XmlSchemaObject child in seq.Items)
                    CollectItemChildren(child, depth, visited, children, choiceGroupCounter);
                break;
            case XmlSchemaChoice choice:
                CollectChoiceChildren(choice, depth, visited, children, choiceGroupCounter);
                break;
            case XmlSchemaAll all:
                foreach (XmlSchemaObject child in all.Items)
                    CollectItemChildren(child, depth, visited, children, choiceGroupCounter);
                break;
        }
    }

    private static void CollectChoiceChildren(
        XmlSchemaChoice choice,
        int depth,
        Dictionary<XmlSchemaComplexType, int> visited,
        List<SchemaNode> children,
        Counter choiceGroupCounter)
    {
        int groupId = choiceGroupCounter.Next();

        for (int optionIdx = 0; optionIdx < choice.Items.Count; optionIdx++)
        {
            var item = choice.Items[optionIdx];
            switch (item)
            {
                case XmlSchemaElement el:
                    children.Add(BuildElementNode(el, depth, visited, choiceGroupCounter,
                        isChoice: true, choiceGroup: groupId, choiceOption: optionIdx));
                    break;
                case XmlSchemaSequence seq:
                    foreach (XmlSchemaObject seqItem in seq.Items)
                    {
                        if (seqItem is XmlSchemaElement seqEl)
                            children.Add(BuildElementNode(seqEl, depth, visited, choiceGroupCounter,
                                isChoice: true, choiceGroup: groupId, choiceOption: optionIdx));
                        else
                            CollectItemChildren(seqItem, depth, visited, children, choiceGroupCounter);
                    }
                    break;
                default:
                    CollectItemChildren(item, depth, visited, children, choiceGroupCounter);
                    break;
            }
        }
    }

    // ──────────────────────────── Restrictions ────────────────────────────

    private static Dictionary<string, string> ExtractRestrictions(XmlSchemaElement element)
    {
        var restrictions = new Dictionary<string, string>();
        var schemaType = element.ElementSchemaType;

        if (schemaType is XmlSchemaSimpleType simpleType)
        {
            ExtractSimpleTypeRestrictions(simpleType, restrictions);
        }
        else if (schemaType is XmlSchemaComplexType complexType)
        {
            // Check simpleContent with restriction
            if (complexType.ContentModel is XmlSchemaSimpleContent simpleContent)
            {
                if (simpleContent.Content is XmlSchemaSimpleContentRestriction scr
                    && complexType.BaseXmlSchemaType is XmlSchemaSimpleType baseST)
                {
                    ExtractSimpleTypeRestrictions(baseST, restrictions);
                }
                else if (simpleContent.Content is XmlSchemaSimpleContentExtension sce
                    && complexType.BaseXmlSchemaType is XmlSchemaSimpleType extBaseST)
                {
                    ExtractSimpleTypeRestrictions(extBaseST, restrictions);
                }
            }
        }

        return restrictions;
    }

    private static void ExtractSimpleTypeRestrictions(
        XmlSchemaSimpleType simpleType,
        Dictionary<string, string> restrictions)
    {
        const string xsdNs = "http://www.w3.org/2001/XMLSchema";
        if (!simpleType.QualifiedName.IsEmpty && simpleType.QualifiedName.Namespace == xsdNs)
            return; // Built-in type, no user-defined restrictions

        if (simpleType.Content is XmlSchemaSimpleTypeRestriction restriction)
        {
            var enums = new List<string>();

            foreach (XmlSchemaFacet facet in restriction.Facets)
            {
                switch (facet)
                {
                    case XmlSchemaEnumerationFacet f:
                        enums.Add(f.Value ?? "");
                        break;
                    case XmlSchemaPatternFacet f:
                        restrictions.TryAdd("pattern", f.Value ?? "");
                        break;
                    case XmlSchemaMaxLengthFacet f:
                        restrictions.TryAdd("maxLength", f.Value ?? "");
                        break;
                    case XmlSchemaMinLengthFacet f:
                        restrictions.TryAdd("minLength", f.Value ?? "");
                        break;
                    case XmlSchemaLengthFacet f:
                        restrictions.TryAdd("length", f.Value ?? "");
                        break;
                    case XmlSchemaMinInclusiveFacet f:
                        restrictions.TryAdd("minInclusive", f.Value ?? "");
                        break;
                    case XmlSchemaMaxInclusiveFacet f:
                        restrictions.TryAdd("maxInclusive", f.Value ?? "");
                        break;
                    case XmlSchemaMinExclusiveFacet f:
                        restrictions.TryAdd("minExclusive", f.Value ?? "");
                        break;
                    case XmlSchemaMaxExclusiveFacet f:
                        restrictions.TryAdd("maxExclusive", f.Value ?? "");
                        break;
                    case XmlSchemaTotalDigitsFacet f:
                        restrictions.TryAdd("totalDigits", f.Value ?? "");
                        break;
                    case XmlSchemaFractionDigitsFacet f:
                        restrictions.TryAdd("fractionDigits", f.Value ?? "");
                        break;
                    case XmlSchemaWhiteSpaceFacet f:
                        restrictions.TryAdd("whiteSpace", f.Value ?? "");
                        break;
                }
            }

            if (enums.Count > 0)
                restrictions["enumeration"] = string.Join(", ", enums);

            // Also walk the base type chain for inherited restrictions
            if (simpleType.BaseXmlSchemaType is XmlSchemaSimpleType baseSimple)
                ExtractSimpleTypeRestrictions(baseSimple, restrictions);
        }

        if (simpleType.Content is XmlSchemaSimpleTypeUnion union)
        {
            var memberNames = new List<string>();
            if (union.MemberTypes is not null)
                foreach (var m in union.MemberTypes)
                    memberNames.Add(CleanTypeName(m));
            if (memberNames.Count > 0)
                restrictions.TryAdd("union", string.Join(" | ", memberNames));
        }

        if (simpleType.Content is XmlSchemaSimpleTypeList list)
        {
            string itemType = list.BaseItemType is not null
                ? CleanTypeName(list.BaseItemType.QualifiedName)
                : CleanTypeName(list.ItemTypeName ?? new System.Xml.XmlQualifiedName());
            if (itemType.Length > 0)
                restrictions.TryAdd("list", itemType);
        }
    }

    // ──────────────────────────── Documentation ────────────────────────────

    private static string? GetDocumentation(XmlSchemaAnnotated annotated)
    {
        if (annotated.Annotation is null) return null;

        foreach (XmlSchemaObject item in annotated.Annotation.Items)
        {
            if (item is XmlSchemaDocumentation doc && doc.Markup is { Length: > 0 } markup)
            {
                var text = string.Join("", markup.Select(n => n?.Value ?? n?.OuterXml ?? "")).Trim();
                return text.Length > 0 ? text : null;
            }
        }

        return null;
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static string CleanTypeName(XmlQualifiedName qname)
    {
        if (qname.IsEmpty) return "";
        return qname.Name;
    }

    private static void CountNodes(
        List<SchemaNode> nodes,
        ref int elements,
        ref int complexTypes,
        ref int simpleTypes)
    {
        foreach (var node in nodes)
        {
            elements++;
            if (node.TypeKind == "complex") complexTypes++;
            else if (node.TypeKind == "simple") simpleTypes++;
            CountNodes(node.Children.Where(c => !c.IsAttribute).ToList(),
                ref elements, ref complexTypes, ref simpleTypes);
        }
    }

    /// <summary>Simple mutable counter for generating unique choice group IDs.</summary>
    private sealed class Counter
    {
        private int _value;
        public int Next() => _value++;
    }
}
