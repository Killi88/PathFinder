namespace PathFinder.Models;

/// <summary>
/// Unified schema node model for both XSD and JSON/YAML Schema visualization.
/// Represents a single element, property, or attribute in a schema hierarchy.
/// </summary>
public sealed class SchemaNode
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string TypeKind { get; init; } = "";
    public string MinOccurs { get; init; } = "1";
    public string MaxOccurs { get; init; } = "1";
    public bool IsRequired { get; init; }
    public bool IsChoice { get; init; }
    public int? ChoiceGroup { get; init; }
    public int? ChoiceOption { get; init; }
    public string? ChoiceKeyword { get; init; }
    public bool IsRecursive { get; init; }
    public bool IsTruncated { get; init; }
    public bool IsAttribute { get; init; }
    public bool IsArrayItem { get; init; }
    public string? Documentation { get; init; }
    public Dictionary<string, string> Restrictions { get; init; } = [];
    public string? Format { get; init; }
    public bool IsExpanded { get; set; } = true;
    public List<SchemaNode> Children { get; } = [];
}
