namespace PathFinder.Models;

public class XPathResultItem
{
    public string XPath { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public int LineNumber { get; set; }

    public override string ToString() => $"[Line {LineNumber}]  {XPath}";
}
