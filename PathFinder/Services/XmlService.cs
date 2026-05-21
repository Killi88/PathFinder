using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using PathFinder.Models;

namespace PathFinder.Services;

public static class XmlService
{
    // Shared safe settings for all XmlReader instances — disables DTD processing and
    // external entity resolution to prevent XXE attacks on untrusted content.
    private static readonly XmlReaderSettings SafeReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    /// <summary>
    /// Loads XML into an <see cref="XmlDocument"/> via a safe <see cref="XmlReader"/>
    /// that prohibits DTDs and external entity resolution.
    /// </summary>
    private static XmlDocument SafeLoadXml(string xml, bool preserveWhitespace = true)
    {
        var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = preserveWhitespace };
        using var reader = XmlReader.Create(new StringReader(xml), SafeReaderSettings);
        doc.Load(reader);
        return doc;
    }
    /// <summary>
    /// Pretty-prints the given XML string with 4-space indentation.
    /// </summary>
    public static string FormatXml(string xml)
    {
        var doc = SafeLoadXml(xml, preserveWhitespace: false);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compresses the given XML string to a single line with no extra whitespace.
    /// </summary>
    public static string MinifyXml(string xml)
    {
        var doc = SafeLoadXml(xml, preserveWhitespace: false);

        var settings = new XmlWriterSettings
        {
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false
        };

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses the XML and returns the XPath of the element whose opening tag
    /// is on or immediately before <paramref name="targetLine"/>.
    /// Returns null if the XML is invalid or no node is found.
    /// </summary>
    public static string? GetXPathAtLine(string xmlText, int targetLine, int targetColumn = 0)
    {
        try
        {
            return ScanForXPathAtLine(xmlText, targetLine, targetColumn);
        }
        catch (XmlException)
        {
            // XML is not currently valid — can't determine XPath
            return null;
        }
    }

    // XmlDocument nodes in .NET 8 do not implement IXmlLineInfo, so we scan
    // directly with XmlReader (which does implement IXmlLineInfo) instead.
    private static string? ScanForXPathAtLine(string xmlText, int targetLine, int targetColumn)
    {
        // Element entries: (path segments, line)
        var allElements = new List<(List<(string name, int idx)> segs, int line)>();
        // Attribute entries: (element path segments, attrName, line, startCol, endCol)
        // endCol is the 1-based column of the attribute's closing quote, used to distinguish
        // cursor-on-attribute from cursor-on-text-content on the same line.
        var allAttrs = new List<(List<(string name, int idx)> elemSegs, string attrName, int line, int col, int endCol)>();

        var lines = xmlText.Split('\n');
        var depthStack = new Stack<(string name, int idx)>();
        var childCounters = new Stack<Dictionary<string, int>>();
        childCounters.Push(new Dictionary<string, int>());

        using (var reader = XmlReader.Create(new StringReader(xmlText), SafeReaderSettings))
        {
            var lineInfo = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string name = reader.Name;
                    bool isEmpty = reader.IsEmptyElement;
                    int line = lineInfo.LineNumber;

                    var counters = childCounters.Peek();
                    counters.TryGetValue(name, out int idx);
                    counters[name] = ++idx;

                    var segs = depthStack.Reverse()
                                         .Concat(new[] { (name, idx) })
                                         .ToList();
                    allElements.Add((segs, line));

                    // Collect attributes with their positions
                    if (reader.HasAttributes)
                    {
                        reader.MoveToFirstAttribute();
                        do
                        {
                            int attrLine = lineInfo.LineNumber;
                            int attrCol = lineInfo.LinePosition;
                            int attrEndCol = FindAttributeEndColumn(lines, attrLine, attrCol);
                            allAttrs.Add((segs, reader.Name, attrLine, attrCol, attrEndCol));
                        } while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }

                    if (!isEmpty)
                    {
                        depthStack.Push((name, idx));
                        childCounters.Push(new Dictionary<string, int>());
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (depthStack.Count > 0) depthStack.Pop();
                    if (childCounters.Count > 1) childCounters.Pop();
                }
            }
        }

        // Find the element with the highest line number that is still <= targetLine
        List<(string name, int idx)>? bestSegs = null;
        int bestLine = 0;
        foreach (var (segs, line) in allElements)
        {
            if (line <= targetLine && line > bestLine)
            {
                bestLine = line;
                bestSegs = segs;
            }
        }

        // Determine max sibling indices so we can omit [n] for unique names
        var maxIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (segs, _) in allElements)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                var (name, idx) = segs[i];
                string parentKey = i == 0
                    ? string.Empty
                    : string.Join("/", segs.Take(i).Select(s => $"{s.name}[{s.idx}]"));
                string key = parentKey + "|" + name;
                if (!maxIndices.TryGetValue(key, out int max) || idx > max)
                    maxIndices[key] = idx;
            }
        }

        // Check for attribute match when column is provided:
        // Only match if the cursor falls within the attribute span (name start → closing quote).
        // This prevents a cursor on text content (after '>') from matching a preceding attribute.
        if (targetColumn > 0)
        {
            var attrMatch = allAttrs
                .Where(a => a.line == targetLine && a.col <= targetColumn && targetColumn <= a.endCol)
                .OrderByDescending(a => a.col)
                .FirstOrDefault();

            if (attrMatch != default)
                return BuildDisplayPath(attrMatch.elemSegs, maxIndices) + "/@" + attrMatch.attrName;
        }

        if (bestSegs is null) return null;
        return BuildDisplayPath(bestSegs, maxIndices);
    }

    private static string BuildDisplayPath(List<(string name, int idx)> segs, Dictionary<string, int> maxIndices)
    {
        var parts = new List<string>(segs.Count);
        for (int i = 0; i < segs.Count; i++)
        {
            var (name, idx) = segs[i];
            string parentKey = i == 0
                ? string.Empty
                : string.Join("/", segs.Take(i).Select(s => $"{s.name}[{s.idx}]"));
            string key = parentKey + "|" + name;
            int maxIdx = maxIndices.TryGetValue(key, out int m) ? m : 1;
            parts.Add(maxIdx > 1 ? $"{name}[{idx}]" : name);
        }
        return "/" + string.Join("/", parts);
    }

    /// <summary>
    /// Executes an XPath expression against the XML string and returns
    /// a list of result items carrying the resolved XPath and line numbers.
    /// </summary>
    public static List<XPathResultItem> ExecuteXPath(string xmlText, string xpathExpression)
    {
        var results = new List<XPathResultItem>();

        // If the expression has no namespace-prefixed steps, evaluate against a
        // namespace-stripped copy so that simple unprefixed paths work on documents
        // that use default namespace declarations (xmlns="...").  Expressions that
        // do carry explicit prefixes (e.g. ns0:Foo) keep the namespace-manager path.
        bool hasPrefixes = Regex.IsMatch(xpathExpression, @"\b\w+:(?!:)");
        string xmlForDoc = hasPrefixes ? xmlText : StripDefaultNamespaces(xmlText);

        var doc = SafeLoadXml(xmlForDoc);

        var nsMgr = BuildNamespaceManager(doc);

        XmlNodeList? nodeList;
        try
        {
            nodeList = doc.SelectNodes(xpathExpression, nsMgr);
        }
        catch (XPathException ex)
        {
            throw new InvalidOperationException($"Invalid XPath expression: {ex.Message}", ex);
        }

        if (nodeList is null) return results;

        // XmlDocument nodes in .NET 8 don't implement IXmlLineInfo, so build a
        // separate indexed-xpath → line map using XmlReader (which does).
        var lineMap = BuildIndexedXPathLineMap(xmlText);

        foreach (XmlNode node in nodeList)
        {
            string indexedXPath = BuildIndexedXPath(node);
            lineMap.TryGetValue(indexedXPath, out int lineNum);

            results.Add(new XPathResultItem
            {
                XPath = BuildXPath(node),
                Preview = BuildPreview(node),
                LineNumber = lineNum
            });
        }

        return results;
    }

    // Scan with XmlReader to build a map of fully-indexed-xpath → line number.
    // E.g. "/root[1]/item[2]" → 5
    private static Dictionary<string, int> BuildIndexedXPathLineMap(string xmlText)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        var depthStack = new Stack<(string name, int idx)>();
        var childCounters = new Stack<Dictionary<string, int>>();
        childCounters.Push(new Dictionary<string, int>());

        using var reader = XmlReader.Create(new StringReader(xmlText), SafeReaderSettings);
        var lineInfo = (IXmlLineInfo)reader;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string name = reader.Name;
                bool isEmpty = reader.IsEmptyElement;
                int line = lineInfo.LineNumber;

                var counters = childCounters.Peek();
                counters.TryGetValue(name, out int idx);
                counters[name] = ++idx;

                var segs = depthStack.Reverse().Concat(new[] { (name, idx) });
                string key = "/" + string.Join("/", segs.Select(s => $"{s.name}[{s.idx}]"));
                map[key] = line;

                if (!isEmpty)
                {
                    depthStack.Push((name, idx));
                    childCounters.Push(new Dictionary<string, int>());
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (depthStack.Count > 0) depthStack.Pop();
                if (childCounters.Count > 1) childCounters.Pop();
            }
        }

        return map;
    }

    // Removes bare default-namespace declarations (xmlns="...") from XML text while keeping
    // explicit-prefix declarations (xmlns:prefix="...").  This allows simple unprefixed XPath
    // expressions to match elements that would otherwise be hidden behind a default namespace.
    private static string StripDefaultNamespaces(string xml) =>
        Regex.Replace(xml, @"\sxmlns=([""'][^""']*[""'])", string.Empty);

    // Collects all namespace declarations from the document and creates an XmlNamespaceManager.
    // This allows XPath expressions that use namespace prefixes to resolve correctly.
    private static XmlNamespaceManager BuildNamespaceManager(XmlDocument doc)
    {
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        if (doc.DocumentElement is null) return nsMgr;

        var nav = doc.CreateNavigator()!;
        nav.MoveToRoot();
        while (nav.MoveToFollowing(System.Xml.XPath.XPathNodeType.Element))
        {
            var nsInScope = nav.GetNamespacesInScope(System.Xml.XmlNamespaceScope.Local);
            foreach (var ns in nsInScope)
                if (!string.IsNullOrEmpty(ns.Key))
                    nsMgr.AddNamespace(ns.Key, ns.Value);
        }

        return nsMgr;
    }

    // Build a fully-indexed XPath from an XmlDocument node, e.g. /root[1]/item[2]
    // Used to look up the node in the XmlReader-derived line map.
    private static string BuildIndexedXPath(XmlNode node)
    {
        var parts = new Stack<string>();
        var current = node;
        while (current is not null and not XmlDocument)
        {
            if (current.NodeType == XmlNodeType.Element)
            {
                int idx = 1;
                var sibling = current.ParentNode?.FirstChild;
                int count = 0;
                while (sibling is not null)
                {
                    if (sibling.NodeType == XmlNodeType.Element && sibling.Name == current.Name)
                    {
                        count++;
                        if (sibling == current) idx = count;
                    }
                    sibling = sibling.NextSibling;
                }
                parts.Push($"{current.Name}[{idx}]");
            }
            current = current.ParentNode;
        }
        return "/" + string.Join("/", parts);
    }
    /// <summary>
    /// Enumerates all elements and attributes in the XML document with their
    /// display XPaths, value previews, and line numbers.
    /// </summary>
    public static List<XPathResultItem> GetAllPaths(string xmlText)
    {
        var results = new List<XPathResultItem>();
        XmlDocument doc;
        try { doc = SafeLoadXml(xmlText); }
        catch { return results; }
        if (doc.DocumentElement is null) return results;

        var lineMap = BuildIndexedXPathLineMap(xmlText);
        WalkXmlNodeForPaths(doc.DocumentElement, lineMap, results);
        return results;
    }

    private static void WalkXmlNodeForPaths(
        XmlElement el,
        Dictionary<string, int> lineMap,
        List<XPathResultItem> results)
    {
        string displayPath = BuildXPath(el);
        string indexedPath = BuildIndexedXPath(el);
        lineMap.TryGetValue(indexedPath, out int lineNum);

        bool hasChildElements = el.ChildNodes.OfType<XmlElement>().Any();
        string trimmedText = el.InnerText.Trim();
        string value = hasChildElements ? "{\u2026}"
                     : trimmedText.Length > 0
                         ? (trimmedText.Length > 80 ? trimmedText[..80] + "\u2026" : trimmedText)
                         : "";

        results.Add(new XPathResultItem
        {
            XPath = displayPath,
            Preview = value,
            LineNumber = lineNum
        });

        foreach (XmlAttribute attr in el.Attributes)
        {
            var attrVal = attr.Value;
            results.Add(new XPathResultItem
            {
                XPath = displayPath + "/@" + attr.Name,
                Preview = attrVal.Length > 80 ? attrVal[..80] + "\u2026" : attrVal,
                LineNumber = lineNum
            });
        }

        foreach (XmlNode child in el.ChildNodes)
        {
            if (child is XmlElement childEl)
                WalkXmlNodeForPaths(childEl, lineMap, results);
        }
    }
    // ──────────────────────────── GenerateSampleXml ──────────────────────────────────

    /// <summary>
    /// Generates a sample XML document from an XSD schema, using representative sample values
    /// for each declared element and attribute.
    /// </summary>
    /// <param name="xsdContent">The XSD schema content as a string.</param>
    /// <param name="xsdFileName">
    /// Optional XSD filename. When provided, an <c>xsi:schemaLocation</c> attribute is added
    /// to the root element.
    /// </param>
    /// <returns>A tab-indented sample XML string.</returns>
    public static string GenerateSampleXml(string xsdContent, string? xsdFileName = null)
    {
        XmlSchema schema;
        using (var reader = XmlReader.Create(new StringReader(xsdContent), SafeReaderSettings))
        {
            schema = XmlSchema.Read(reader, null)
                ?? throw new InvalidOperationException("Failed to read XSD schema.");
        }

        var schemaSet = new XmlSchemaSet { XmlResolver = null };
        schemaSet.Add(schema);
        schemaSet.Compile();

        // Find first global element
        XmlSchemaElement? rootElement = null;
        foreach (XmlSchemaElement el in schemaSet.GlobalElements.Values)
        {
            rootElement = el;
            break;
        }
        if (rootElement is null)
            throw new InvalidOperationException("No global element found in XSD schema.");

        string targetNamespace = schema.TargetNamespace ?? string.Empty;
        bool elementFormUnqualified = schema.ElementFormDefault != XmlSchemaForm.Qualified;

        var sb = new StringBuilder();
        var writerSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8
        };

        // Track how many times each named complex type is currently on the call stack.
        // count 0 (not present): first visit — generate all children (mandatory + optional).
        // count 1: second visit (direct ancestor) — generate all children again.
        // count ≥2: would recurse a third level — skip element entirely.
        var visitedTypes = new Dictionary<XmlSchemaComplexType, int>(ReferenceEqualityComparer.Instance);

        using (var writer = XmlWriter.Create(sb, writerSettings))
        {
            writer.WriteStartDocument();

            // Root element — use n1: prefix when the schema has a target namespace
            string rootName = rootElement.Name
                ?? throw new InvalidOperationException("Root element has no name.");
            string prefix = string.IsNullOrEmpty(targetNamespace) ? "" : "n1";
            if (!string.IsNullOrEmpty(prefix))
                writer.WriteStartElement(prefix, rootName, targetNamespace);
            else
                writer.WriteStartElement(rootName);

            // xmlns:xsi declaration (before regular attributes, matching convention)
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

            if (rootElement.ElementSchemaType is XmlSchemaComplexType rootComplexType)
            {
                WriteXsdAttributes(writer, rootComplexType);

                // xsi:schemaLocation after regular schema attributes
                if (!string.IsNullOrEmpty(xsdFileName) && !string.IsNullOrEmpty(targetNamespace))
                    writer.WriteAttributeString("xsi", "schemaLocation",
                        "http://www.w3.org/2001/XMLSchema-instance",
                        $"{targetNamespace} {xsdFileName}");

                bool isNamedRoot = !string.IsNullOrEmpty(rootComplexType.Name);
                if (isNamedRoot) visitedTypes[rootComplexType] = 1;
                WriteXsdComplexTypeContent(writer, rootComplexType, elementFormUnqualified,
                    targetNamespace, visitedTypes);
                if (isNamedRoot) visitedTypes.Remove(rootComplexType);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a sample value for the given compiled schema type, respecting facets
    /// (enumeration, maxLength, minLength, length, minInclusive, maxInclusive,
    /// fractionDigits). Falls back to <see cref="GetXsdSampleValue"/> when no
    /// facet information is available.
    /// </summary>
    private static string GetXsdSampleValueFromType(XmlSchemaType? schemaType, XmlQualifiedName fallbackTypeName)
    {
        if (schemaType is XmlSchemaSimpleType simpleType)
            return GetXsdSampleValueFromSimpleType(simpleType);
        return GetXsdSampleValue(fallbackTypeName);
    }

    private static string GetXsdSampleValueFromSimpleType(XmlSchemaSimpleType simpleType)
    {
        // Built-in XSD primitive types (xs:string, xs:dateTime, xs:unsignedByte, …) already
        // have well-known sample values in GetXsdSampleValue; skip facet analysis for them.
        const string xsdNs = "http://www.w3.org/2001/XMLSchema";
        if (!simpleType.QualifiedName.IsEmpty && simpleType.QualifiedName.Namespace == xsdNs)
            return GetXsdSampleValue(simpleType.QualifiedName);

        if (simpleType.Content is XmlSchemaSimpleTypeRestriction restriction)
        {
            string? firstEnum = null;
            string? firstPattern = null;
            int? maxLen = null;
            int? minLen = null;
            int? exactLen = null;
            decimal? minInclusive = null;
            decimal? maxInclusive = null;
            int? fractionDigits = null;

            foreach (XmlSchemaFacet facet in restriction.Facets)
            {
                switch (facet)
                {
                    case XmlSchemaEnumerationFacet f when firstEnum is null:
                        firstEnum = f.Value;
                        break;
                    case XmlSchemaPatternFacet f when firstPattern is null:
                        firstPattern = f.Value;
                        break;
                    case XmlSchemaMaxLengthFacet f when int.TryParse(f.Value, out int v):
                        maxLen = v;
                        break;
                    case XmlSchemaMinLengthFacet f when int.TryParse(f.Value, out int v):
                        minLen = v;
                        break;
                    case XmlSchemaLengthFacet f when int.TryParse(f.Value, out int v):
                        exactLen = v;
                        break;
                    case XmlSchemaMinInclusiveFacet f when decimal.TryParse(f.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v):
                        minInclusive = v;
                        break;
                    case XmlSchemaMaxInclusiveFacet f when decimal.TryParse(f.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v):
                        maxInclusive = v;
                        break;
                    case XmlSchemaFractionDigitsFacet f when int.TryParse(f.Value, out int v):
                        fractionDigits = v;
                        break;
                }
            }

            // Enumeration takes highest priority — any listed value is always valid
            if (firstEnum is not null)
                return firstEnum;

            // Get base value, recursing into chained restrictions when applicable
            var baseSchemaType = simpleType.BaseXmlSchemaType;
            string baseValue = baseSchemaType is XmlSchemaSimpleType baseSimple
                ? GetXsdSampleValueFromSimpleType(baseSimple)
                : GetXsdSampleValue(restriction.BaseTypeName);

            // Determine the primitive XSD type by walking the base type chain
            var primitiveType = GetXsdPrimitiveType(simpleType);

            if (IsNumericLikeType(primitiveType))
            {
                // For numeric types, minInclusive (if present) takes priority over pattern —
                // e.g. n..4 has both \d{1,4} and minInclusive=1; use 1 not 0.
                if (minInclusive.HasValue)
                {
                    decimal num = minInclusive.Value;
                    if (maxInclusive.HasValue && num > maxInclusive.Value) num = maxInclusive.Value;
                    int fracDig = fractionDigits ?? 0;
                    if (fracDig > 0)
                        return num.ToString("F" + fracDig, CultureInfo.InvariantCulture);
                    return ((long)num).ToString(CultureInfo.InvariantCulture);
                }

                // Pattern on a numeric type (e.g. n4: \d{4} requires exactly 4 digits).
                if (firstPattern is not null)
                {
                    string patternSample = GenerateSampleFromXsdPattern(firstPattern);
                    if (patternSample.Length > 0)
                        return patternSample;
                }

                decimal numDefault = 0m;
                if (maxInclusive.HasValue && numDefault > maxInclusive.Value) numDefault = maxInclusive.Value;
                int fracDigDefault = fractionDigits ?? 0;
                if (fracDigDefault > 0)
                    return numDefault.ToString("F" + fracDigDefault, CultureInfo.InvariantCulture);
                return ((long)numDefault).ToString(CultureInfo.InvariantCulture);
            }

            // For string types: pattern facet generates a valid sample value directly.
            // Checked before length adjustments because patterns encode the required format.
            if (firstPattern is not null)
            {
                string patternSample = GenerateSampleFromXsdPattern(firstPattern);
                if (patternSample.Length > 0)
                    return patternSample;
            }

            if (IsStringLikeType(primitiveType))
            {
                if (exactLen.HasValue)
                    return PadOrTruncate("String", exactLen.Value);
                if (maxLen.HasValue && baseValue.Length > maxLen.Value)
                    return baseValue[..maxLen.Value];
                if (minLen.HasValue && baseValue.Length < minLen.Value)
                    return baseValue.PadRight(minLen.Value, 'X');
                return baseValue;
            }

            return baseValue;
        }

        if (simpleType.Content is XmlSchemaSimpleTypeUnion union)
        {
            if (union.BaseTypes.Count > 0 && union.BaseTypes[0] is XmlSchemaSimpleType firstMember)
                return GetXsdSampleValueFromSimpleType(firstMember);
            if (union.MemberTypes is { Length: > 0 })
                return GetXsdSampleValue(union.MemberTypes[0]);
        }

        if (simpleType.Content is XmlSchemaSimpleTypeList list)
        {
            if (list.BaseItemType is not null)
                return GetXsdSampleValueFromSimpleType(list.BaseItemType);
            if (!list.ItemTypeName.IsEmpty)
                return GetXsdSampleValue(list.ItemTypeName);
        }

        // Built-in primitive type (no faceted content) or unrecognised
        return GetXsdSampleValue(simpleType.QualifiedName);
    }

    // Walks the base type chain to find the primitive XSD built-in type name.
    private static XmlQualifiedName GetXsdPrimitiveType(XmlSchemaSimpleType simpleType)
    {
        const string xsdNs = "http://www.w3.org/2001/XMLSchema";
        XmlSchemaType? t = simpleType;
        while (t != null)
        {
            if (t is XmlSchemaSimpleType st && !st.QualifiedName.IsEmpty
                    && st.QualifiedName.Namespace == xsdNs)
                return st.QualifiedName;
            t = t.BaseXmlSchemaType;
        }
        return XmlQualifiedName.Empty;
    }

    private static bool IsStringLikeType(XmlQualifiedName typeName) =>
        typeName.Name is "string" or "normalizedString" or "token" or "Name" or
        "NCName" or "ID" or "IDREF" or "NMTOKEN" or "language" or
        "anyURI" or "base64Binary" or "hexBinary";

    private static bool IsNumericLikeType(XmlQualifiedName typeName) =>
        typeName.Name is "int" or "integer" or "long" or "short" or "byte" or
        "unsignedByte" or "unsignedInt" or "unsignedShort" or "unsignedLong" or
        "positiveInteger" or "nonNegativeInteger" or "negativeInteger" or
        "nonPositiveInteger" or "decimal" or "float" or "double";

    private static string PadOrTruncate(string value, int length)
    {
        if (length <= 0) return string.Empty;
        return value.Length >= length ? value[..length] : value.PadRight(length, 'X');
    }

    /// <summary>
    /// Generates a sample string that satisfies an XSD pattern facet by interpreting
    /// common XML Schema regular-expression constructs.  Not a full regex engine —
    /// handles the subset seen in real-world XSD schemas: escape sequences (\d, \w, \s,
    /// \r, \n, \t), character classes ([A-Z], [0-9], [^ioOI], …), quantifiers ({n},
    /// {n,m}, +, *, ?), groups, and alternation (uses first alternative).
    /// </summary>
    private static string GenerateSampleFromXsdPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Empty;
        var sb = new StringBuilder();
        int i = 0;
        int len = pattern.Length;

        while (i < len)
        {
            // Parse one atom (base token before the optional quantifier)
            string atomSample;
            int atomEnd;

            if (pattern[i] == '\\' && i + 1 < len)
            {
                // XSD escape sequence: \d \D \w \W \s \S \r \n \t \i \c (XML Name chars), etc.
                atomSample = pattern[i + 1] switch
                {
                    'd' => "0",
                    'D' => "A",
                    'w' => "A",
                    'W' => " ",
                    's' => " ",
                    'S' => "A",
                    'r' => "\r",
                    'n' => "\n",
                    't' => "\t",
                    'i' => "A",   // initial XML name char
                    'c' => "A",   // subsequent XML name char
                    _ => pattern[i + 1].ToString()
                };
                atomEnd = i + 2;
            }
            else if (pattern[i] == '[')
            {
                // Character class — find closing ] respecting escapes and ] as first char
                int j = i + 1;
                if (j < len && pattern[j] == '^') j++;       // negation marker
                if (j < len && pattern[j] == ']') j++;       // ] immediately after [ or [^ is literal
                while (j < len && pattern[j] != ']') j++;
                string cls = pattern[i..(j + 1)];
                atomSample = SampleFromXsdCharClass(cls);
                atomEnd = j + 1;
            }
            else if (pattern[i] == '(')
            {
                // Group — find matching ), take first alternative
                int depth = 1, j = i + 1;
                int firstAltPos = -1;
                while (j < len && depth > 0)
                {
                    char ch = pattern[j];
                    if (ch == '\\') { j += 2; continue; }          // skip escaped char
                    if (ch == '(') depth++;
                    else if (ch == ')') depth--;
                    else if (depth == 1 && ch == '|' && firstAltPos < 0) firstAltPos = j;
                    j++;
                }
                int innerEnd = firstAltPos >= 0 ? firstAltPos : j - 1;
                string inner = pattern[(i + 1)..innerEnd];
                atomSample = GenerateSampleFromXsdPattern(inner);
                atomEnd = j;
            }
            else if (pattern[i] == '.')
            {
                atomSample = "A";
                atomEnd = i + 1;
            }
            else if (pattern[i] is '+' or '*' or '?' or '{' or '}' or '|' or '$' or '^')
            {
                // Stray quantifier / anchor — skip
                i++;
                continue;
            }
            else
            {
                // Literal character
                atomSample = pattern[i].ToString();
                atomEnd = i + 1;
            }

            // Read quantifier that follows the atom
            int minCount = 1;
            int j2 = atomEnd;
            if (j2 < len)
            {
                if (pattern[j2] == '{')
                {
                    int closeBrace = pattern.IndexOf('}', j2 + 1);
                    if (closeBrace > 0)
                    {
                        string quant = pattern[(j2 + 1)..closeBrace];
                        int commaIdx = quant.IndexOf(',');
                        if (commaIdx >= 0)
                        {
                            // {n,m} or {n,} — use minimum (n)
                            if (int.TryParse(quant[..commaIdx].Trim(), out int n)) minCount = n;
                        }
                        else
                        {
                            // {n} — exactly n
                            if (int.TryParse(quant.Trim(), out int n)) minCount = n;
                        }
                        atomEnd = closeBrace + 1;
                    }
                }
                else if (pattern[j2] == '+') { minCount = 1; atomEnd = j2 + 1; }
                else if (pattern[j2] == '*') { minCount = 1; atomEnd = j2 + 1; }  // use 1 for sample
                else if (pattern[j2] == '?') { minCount = 0; atomEnd = j2 + 1; }  // optional — omit
            }

            for (int k = 0; k < minCount; k++)
                sb.Append(atomSample);
            i = atomEnd;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Given an XSD character class string like <c>[A-Z]</c>, <c>[0-9]</c>, or
    /// <c>[^ioOI]</c>, returns a single representative sample character.
    /// </summary>
    private static string SampleFromXsdCharClass(string cls)
    {
        if (cls.Length < 2) return "A";
        bool negated = cls.Length > 2 && cls[1] == '^';
        // inner is everything between the outer [ and ]
        int start = negated ? 2 : 1;
        int end = cls.Length - 1; // index of ']'
        if (start >= end) return negated ? "A" : "A";
        string inner = cls[start..end];

        if (!negated)
        {
            // Return the first character from the first range or literal in the class
            int i = 0;
            while (i < inner.Length)
            {
                if (inner[i] == '\\' && i + 1 < inner.Length)
                {
                    // Escape in char class
                    char sample = inner[i + 1] switch { 'd' => '0', 'w' => 'A', 's' => ' ', _ => inner[i + 1] };
                    return sample.ToString();
                }
                if (i + 2 < inner.Length && inner[i + 1] == '-')
                    return inner[i].ToString();   // start of a range, e.g. A-Z → return A
                return inner[i].ToString();       // literal character
            }
            return "A";
        }
        else
        {
            // Negated: build the exclusion set, then return first non-excluded letter
            var excluded = new HashSet<char>();
            int i = 0;
            while (i < inner.Length)
            {
                if (inner[i] == '\\' && i + 1 < inner.Length)
                {
                    // \d excludes digits, \r/\n/\t exclude their chars
                    char esc = inner[i + 1];
                    if (esc == 'd') for (char c = '0'; c <= '9'; c++) excluded.Add(c);
                    else if (esc == 'r') excluded.Add('\r');
                    else if (esc == 'n') excluded.Add('\n');
                    else if (esc == 't') excluded.Add('\t');
                    else excluded.Add(esc);
                    i += 2;
                }
                else if (i + 2 < inner.Length && inner[i + 1] == '-')
                {
                    char from = inner[i], to = inner[i + 2];
                    for (char c = from; c <= to; c++) excluded.Add(c);
                    i += 3;
                }
                else
                {
                    excluded.Add(inner[i]);
                    i++;
                }
            }
            // Return first uppercase letter not excluded
            for (char c = 'A'; c <= 'Z'; c++)
                if (!excluded.Contains(c)) return c.ToString();
            for (char c = '0'; c <= '9'; c++)
                if (!excluded.Contains(c)) return c.ToString();
            return "A";
        }
    }

    private static string GetXsdSampleValue(XmlQualifiedName typeName) =>
        typeName.Name switch
        {
            "string" or "normalizedString" or "token" or "Name" or
            "NCName" or "ID" or "IDREF" or "NMTOKEN" or "language" => "String",
            "unsignedByte" => "255",
            "unsignedInt" or "unsignedShort" or "unsignedLong" or
            "positiveInteger" or "nonNegativeInteger" => "0",
            "int" or "integer" or "long" or "short" or "byte" or
            "negativeInteger" or "nonPositiveInteger" => "0",
            "decimal" or "float" or "double" => "0",
            "boolean" => "false",
            "dateTime" => "2001-12-17T09:30:47Z",
            "date" => "2001-12-17",
            "time" => "09:30:47Z",
            "duration" => "P1Y",
            "anyURI" => "http://example.com",
            "base64Binary" => "AAAA",
            "hexBinary" => "00",
            _ => "String"
        };

    private static void WriteXsdAttributes(XmlWriter writer, XmlSchemaComplexType complexType)
    {
        // Simple content: attributes live on the extension element
        if (complexType.ContentModel is XmlSchemaSimpleContent { Content: XmlSchemaSimpleContentExtension ext })
        {
            foreach (XmlSchemaObject obj in ext.Attributes)
                if (obj is XmlSchemaAttribute attr) WriteXsdAttribute(writer, attr);
            return;
        }

        // Use post-compilation AttributeUses so that attributes inherited from base types
        // via xs:complexContent > xs:extension are included (complexType.Attributes only
        // contains attributes declared directly on the outer xs:complexType element).
        foreach (XmlSchemaAttribute attr in complexType.AttributeUses.Values)
            WriteXsdAttribute(writer, attr);
    }

    private static void WriteXsdAttribute(XmlWriter writer, XmlSchemaAttribute attr)
    {
        if (attr.Name is null) return;
        writer.WriteAttributeString(attr.Name, GetXsdSampleValueFromType(attr.AttributeSchemaType, attr.SchemaTypeName));
    }

    private static void WriteXsdComplexTypeContent(
        XmlWriter writer,
        XmlSchemaComplexType complexType,
        bool elementFormUnqualified,
        string targetNamespace,
        Dictionary<XmlSchemaComplexType, int> visitedTypes)
    {
        // Simple content — write text value; attributes already written by WriteXsdAttributes
        if (complexType.ContentModel is XmlSchemaSimpleContent { Content: XmlSchemaSimpleContentExtension simpleExt })
        {
            writer.WriteString(GetXsdSampleValueFromType(complexType.BaseXmlSchemaType, simpleExt.BaseTypeName));
            return;
        }

        // Use the post-compilation ContentTypeParticle so that xs:complexContent > xs:extension
        // types emit both the base type's particle AND the extension's particle (merged by the
        // schema compiler). complexType.Particle is null for derived types.
        WriteXsdParticle(writer, complexType.ContentTypeParticle, elementFormUnqualified, targetNamespace, visitedTypes);
    }

    private static void WriteXsdParticle(
        XmlWriter writer,
        XmlSchemaParticle? particle,
        bool elementFormUnqualified,
        string targetNamespace,
        Dictionary<XmlSchemaComplexType, int> visitedTypes)
    {
        if (particle is null) return;

        switch (particle)
        {
            case XmlSchemaSequence seq:
                foreach (XmlSchemaObject item in seq.Items)
                    WriteXsdItem(writer, item, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaAll all:
                foreach (XmlSchemaObject item in all.Items)
                    WriteXsdItem(writer, item, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaChoice { Items.Count: > 0 } choice:
                WriteXsdItem(writer, choice.Items[0], elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaElement el:
                WriteXsdElementSample(writer, el, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
        }
    }

    private static void WriteXsdItem(
        XmlWriter writer,
        XmlSchemaObject item,
        bool elementFormUnqualified,
        string targetNamespace,
        Dictionary<XmlSchemaComplexType, int> visitedTypes)
    {
        switch (item)
        {
            case XmlSchemaElement el:
                WriteXsdElementSample(writer, el, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaSequence seq:
                foreach (XmlSchemaObject child in seq.Items)
                    WriteXsdItem(writer, child, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaChoice { Items.Count: > 0 } choice:
                WriteXsdItem(writer, choice.Items[0], elementFormUnqualified, targetNamespace, visitedTypes);
                break;
            case XmlSchemaAll all:
                foreach (XmlSchemaObject child in all.Items)
                    WriteXsdItem(writer, child, elementFormUnqualified, targetNamespace, visitedTypes);
                break;
        }
    }

    private static void WriteXsdElementSample(
        XmlWriter writer,
        XmlSchemaElement element,
        bool elementFormUnqualified,
        string targetNamespace,
        Dictionary<XmlSchemaComplexType, int> visitedTypes)
    {
        if (element.Name is null) return;

        var schemaType = element.ElementSchemaType;
        if (schemaType is XmlSchemaComplexType complexType)
        {
            bool isNamedType = !string.IsNullOrEmpty(complexType.Name);
            visitedTypes.TryGetValue(complexType, out int currentCount);

            // Cycle detection: skip before WriteStartElement so we never write an empty
            // element that violates mandatory-child constraints.
            // count 0 → first visit: write element + all children (mandatory + optional)
            // count 1 → second visit: write element + all children again
            // count ≥2 → would recurse a third level: skip entirely
            if (isNamedType && currentCount >= 2)
                return;

            if (elementFormUnqualified || string.IsNullOrEmpty(targetNamespace))
                writer.WriteStartElement(element.Name);
            else
                writer.WriteStartElement(element.Name, targetNamespace);

            if (isNamedType)
                visitedTypes[complexType] = currentCount + 1;

            WriteXsdAttributes(writer, complexType);
            WriteXsdComplexTypeContent(writer, complexType, elementFormUnqualified,
                targetNamespace, visitedTypes);

            if (isNamedType)
            {
                if (currentCount == 0)
                    visitedTypes.Remove(complexType);
                else
                    visitedTypes[complexType] = currentCount;
            }
        }
        else
        {
            if (elementFormUnqualified || string.IsNullOrEmpty(targetNamespace))
                writer.WriteStartElement(element.Name);
            else
                writer.WriteStartElement(element.Name, targetNamespace);

            // Simple or primitive type — respect XSD facets (maxLength, enumeration, etc.)
            writer.WriteString(GetXsdSampleValueFromType(schemaType, element.SchemaTypeName));
        }

        writer.WriteEndElement();
    }

    // ──────────────────────────────────── helpers ────────────────────────────────────

#pragma warning disable CS0618 // XmlTextReader is obsolete but is the only way to get IXmlLineInfo on nodes after doc.Load()
    private static XmlDocument LoadWithLineInfo(string xmlText)
    {
        var doc = new XmlDocument { XmlResolver = null };
        using var reader = new XmlTextReader(new StringReader(xmlText))
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        doc.Load(reader);
        return doc;
    }
#pragma warning restore CS0618

    // Returns the 1-based column of the closing quote of an attribute whose name starts at
    // (line, startCol) in the raw XML lines array.  Falls back to startCol on any parse failure
    // so that callers can still use startCol ≤ cursor as a conservative fallback.
    private static int FindAttributeEndColumn(string[] lines, int line, int startCol)
    {
        if (line < 1 || line > lines.Length) return startCol;
        var lineText = lines[line - 1];
        int pos = startCol - 1; // convert to 0-based

        // Skip attribute name chars until '='
        while (pos < lineText.Length && lineText[pos] != '=') pos++;
        if (pos >= lineText.Length) return startCol;
        pos++; // skip '='

        // Skip optional whitespace around '='
        while (pos < lineText.Length && lineText[pos] is ' ' or '\t') pos++;
        if (pos >= lineText.Length) return startCol;

        // Expect opening quote
        char quote = lineText[pos];
        if (quote is not '"' and not '\'') return startCol;
        pos++; // skip opening quote

        // Scan to matching closing quote (handles entity refs since we scan raw text)
        while (pos < lineText.Length && lineText[pos] != quote) pos++;
        if (pos >= lineText.Length) return startCol;

        return pos + 1; // convert back to 1-based
    }

    /// <summary>
    /// Builds an absolute XPath string for the given node,
    /// e.g. /catalog/book[2]/title
    /// </summary>
    public static string BuildXPath(XmlNode node)
    {
        var parts = new Stack<string>();
        var current = node;

        while (current is not null and not XmlDocument)
        {
            parts.Push(GetXPathSegment(current));
            current = current.ParentNode;
        }

        return "/" + string.Join("/", parts);
    }

    private static string GetXPathSegment(XmlNode node)
    {
        if (node is XmlAttribute attr)
            return "@" + attr.Name;

        if (node.NodeType != XmlNodeType.Element)
            return node.Name;

        var parent = node.ParentNode;
        if (parent is null or XmlDocument)
            return node.Name;

        int index = 0, count = 0;
        foreach (XmlNode sibling in parent.ChildNodes)
        {
            if (sibling.NodeType == XmlNodeType.Element && sibling.Name == node.Name)
            {
                count++;
                if (sibling == node) index = count;
            }
        }

        return count > 1 ? $"{node.Name}[{index}]" : node.Name;
    }

    private static string BuildPreview(XmlNode node)
    {
        if (node is XmlElement element)
        {
            var sb = new StringBuilder();
            sb.Append('<').Append(element.Name);
            foreach (XmlAttribute a in element.Attributes)
                sb.Append($" {a.Name}=\"{a.Value}\"");

            if (!element.HasChildNodes)
            {
                sb.Append(" />");
                return sb.ToString();
            }

            if (element.ChildNodes.Count == 1 && element.FirstChild is XmlText txt)
            {
                var val = txt.Value?.Trim() ?? string.Empty;
                if (val.Length > 80) val = val[..80] + "…";
                return val;
            }

            return "{…}";
        }

        if (node is XmlAttribute a2)
            return $"@{a2.Name} = \"{a2.Value}\"";

        if (node is XmlText t)
        {
            var v = t.Value?.Trim() ?? string.Empty;
            if (v.Length > 70) v = v[..70] + "…";
            return $"[text: {v}]";
        }

        var outer = node.OuterXml;
        return outer.Length > 100 ? outer[..100] + "…" : outer;
    }

    // ──────────────────────────── XSD validation ─────────────────────────────

    /// <summary>
    /// Validates an XML document against an XSD schema.
    /// </summary>
    /// <param name="xmlContent">The XML content to validate.</param>
    /// <param name="xsdContent">The XSD schema content to validate against.</param>
    /// <returns>
    /// An empty list when the XML is valid; otherwise a list of error strings,
    /// each formatted as <c>"Line N: message"</c> for compatibility with
    /// the Messages panel navigation.
    /// </returns>
    public static IReadOnlyList<string> ValidateXmlAgainstXsd(string xmlContent, string xsdContent)
    {
        XmlSchema schema;
        using (var schemaReader = XmlReader.Create(new StringReader(xsdContent), SafeReaderSettings))
        {
            schema = XmlSchema.Read(schemaReader, null)
                ?? throw new InvalidOperationException("Failed to read XSD schema.");
        }

        var schemaSet = new XmlSchemaSet { XmlResolver = null };
        schemaSet.Add(schema);
        schemaSet.Compile();

        var errors = new List<string>();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            XmlResolver = null,
            Schemas = schemaSet,
            DtdProcessing = DtdProcessing.Prohibit,
        };
        settings.ValidationEventHandler += (_, e) =>
        {
            int line = e.Exception?.LineNumber ?? 0;
            errors.Add(line > 0 ? $"Line {line}: {e.Message}" : e.Message);
        };

        string? rootLocalName = null;
        string? rootNamespace = null;

        using var xmlReader = XmlReader.Create(new StringReader(xmlContent), settings);
        while (xmlReader.Read())
        {
            if (rootLocalName is null && xmlReader.NodeType == XmlNodeType.Element)
            {
                rootLocalName = xmlReader.LocalName;
                rootNamespace = xmlReader.NamespaceURI ?? string.Empty;
            }
        }

        // If no schema errors were raised but the root element doesn't correspond to any
        // global element in the schema, the validator silently skipped the document
        // (lax validation — no matching namespace). Report it explicitly.
        if (errors.Count == 0 && rootLocalName is not null)
        {
            var qualifiedName = new XmlQualifiedName(rootLocalName, rootNamespace ?? string.Empty);
            if (!schemaSet.GlobalElements.Contains(qualifiedName))
            {
                string display = string.IsNullOrEmpty(rootNamespace)
                    ? rootLocalName
                    : $"{{{rootNamespace}}}{rootLocalName}";
                errors.Add($"The XML root element '{display}' does not match any element declared in the XSD schema.");
            }
        }

        return errors;
    }
}
