namespace PathFinder.Models;

public sealed class GridNode
{
    public string Label { get; init; } = "";
    public string ValueDisplay { get; init; } = "";
    public string LabelColor { get; init; } = "#D4D4D4";
    public string ValueColor { get; init; } = "#CE9178";
    public bool IsExpanded { get; init; } = true;
    public List<GridNode> Children { get; } = [];
}
