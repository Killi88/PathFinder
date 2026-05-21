namespace PathFinder.Models;

public class MessageItem
{
    public string Message { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public bool IsSuccess { get; set; }

    /// <summary>EDIFACT directory code (e.g. "D96A") when this error relates to a code list.</summary>
    public string? CodeListDirectory { get; set; }

    /// <summary>Data element ID (e.g. "3035") when this error relates to a code list.</summary>
    public string? CodeListElementId { get; set; }

    /// <summary>Human-readable element name (e.g. "PARTY QUALIFIER").</summary>
    public string? CodeListElementName { get; set; }

    /// <summary>Segment tag (e.g. "NAD") when this error relates to a code list.</summary>
    public string? CodeListSegmentTag { get; set; }

    public bool HasCodeList => CodeListDirectory is not null && CodeListElementId is not null;

    public string Display => LineNumber is int ln ? $"Line {ln}: {Message}" : Message;
}
