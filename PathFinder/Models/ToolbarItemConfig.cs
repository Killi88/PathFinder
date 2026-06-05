namespace PathFinder;

public record ToolbarItemConfig(string Id)
{
    public const string SeparatorId = "---";
    public bool IsSeparator => Id == SeparatorId;
}
