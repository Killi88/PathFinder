using System.IO;
using PathFinder.Services;

namespace PathFinder.Tests;

public class EdifactServiceTests
{
    // ── inline data tests ──────────────────────────────────────────────

    // Single EDIFACT message with no line breaks
    private const string SampleEdi =
        "UNB+UNOA:3+SENDER:14+RECEIVER:14+190101:1200+1'" +
        "UNH+1+ORDERS:D:96A:UN'" +
        "BGM+220+4711+9'" +
        "DTM+137:20190101:102'" +
        "UNT+4+1'" +
        "UNZ+1+1'";

    [Fact]
    public void FormatEdi_SingleLineInput_SplitsIntoSegments()
    {
        var result = EdifactService.FormatEdi(SampleEdi);
        var lines = result.Split('\n');

        // Each segment is on its own line
        Assert.Equal(6, lines.Length);
        Assert.Equal("UNB+UNOA:3+SENDER:14+RECEIVER:14+190101:1200+1'", lines[0]);
        Assert.Equal("UNH+1+ORDERS:D:96A:UN'", lines[1]);
        Assert.Equal("BGM+220+4711+9'", lines[2]);
        Assert.Equal("DTM+137:20190101:102'", lines[3]);
        Assert.Equal("UNT+4+1'", lines[4]);
        Assert.Equal("UNZ+1+1'", lines[5]);
    }

    [Fact]
    public void FormatEdi_AlreadyFormatted_ProducesSameOutput()
    {
        // Pre-formatted input (newline after each segment terminator)
        var preFormatted = "UNB+UNOA:3'\nUNH+1+ORDERS:D:96A:UN'\nUNZ+1+1'";
        var result = EdifactService.FormatEdi(preFormatted);

        Assert.Equal("UNB+UNOA:3'\nUNH+1+ORDERS:D:96A:UN'\nUNZ+1+1'", result);
    }

    [Fact]
    public void FormatEdi_EscapedQuote_IsNotSplitAsTerminator()
    {
        // ?' is a release sequence — the quote is a literal value, not a segment terminator
        const string edi = "TST+value?'with?'quotes+normal'";
        var result = EdifactService.FormatEdi(edi);
        var lines = result.Split('\n');

        // Only one segment (the outer ' is the real terminator)
        Assert.Single(lines);
        Assert.Equal("TST+value?'with?'quotes+normal'", lines[0]);
    }

    [Fact]
    public void FormatEdi_DoubleReleaseChar_HandledCorrectly()
    {
        // ?? means a literal ? — the second ? is the next char consumed by release
        const string edi = "TST+value??+more'";
        var result = EdifactService.FormatEdi(edi);

        Assert.Equal("TST+value??+more'", result);
    }

    [Fact]
    public void FormatEdi_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EdifactService.FormatEdi(string.Empty));
    }

    [Fact]
    public void FormatEdi_TrailingWhitespaceAfterLastSegment_IsTrimmed()
    {
        const string edi = "UNB+TEST'\n\n";
        var result = EdifactService.FormatEdi(edi);

        Assert.Equal("UNB+TEST'", result);
    }

    [Fact]
    public void FormatEdi_ExistingCarriageReturns_AreRemoved()
    {
        const string edi = "UNB+TEST'\r\nUNZ+1'";
        var result = EdifactService.FormatEdi(edi);
        var lines = result.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal("UNB+TEST'", lines[0]);
        Assert.Equal("UNZ+1'", lines[1]);
    }

    // ── file-based tests ───────────────────────────────────────────────
    // edifact.edi      — well-formatted (one segment per line, LF endings)
    // edifactBadFormat.edi — all 9 segments on a single line

    private static string EdifactFile =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "edifact.edi");

    private static string EdifactBadFormatFile =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "edifactBadFormat.edi");

    // Expected segment order and content shared by both file tests
    // Line numbers (1-based) in edifact.edi:
    //  1: UNH+1+IFTMCS:D:96A:UN'
    //  2: BGM+701+BL-112233'
    //  3: DTM+137:20240725:102'
    //  4: NAD+CZ+Acme Goods'
    //  5: NAD+CN+Best Retail'
    //  6: NAD+CA+Swift Trucking'
    //  7: GID+1+10:PA'
    //  8: MEA+WT+AAB+KGM:2268'
    //  9: UNT+9+1'
    private static readonly string[] ExpectedSegments =
    [
        "UNH+1+IFTMCS:D:96A:UN'",
        "BGM+701+BL-112233'",
        "DTM+137:20240725:102'",
        "NAD+CZ+Acme Goods'",
        "NAD+CN+Best Retail'",
        "NAD+CA+Swift Trucking'",
        "GID+1+10:PA'",
        "MEA+WT+AAB+KGM:2268'",
        "UNT+9+1'",
    ];

    [Fact]
    public void FormatEdi_EdifactFile_ParsesAndFormatsWithoutError()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.FormatEdi(content); // must not throw
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FormatEdi_EdifactFile_IsIdempotent()
    {
        // edifact.edi is already one segment per line — formatting it again should produce the same output
        var content = File.ReadAllText(EdifactFile);
        var firstPass = EdifactService.FormatEdi(content);
        var secondPass = EdifactService.FormatEdi(firstPass);

        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void FormatEdi_EdifactFile_ProducesNineSegments()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.FormatEdi(content);
        var lines = result.Split('\n');

        Assert.Equal(9, lines.Length);
    }

    [Fact]
    public void FormatEdi_EdifactFile_FirstSegmentIsUNH()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.FormatEdi(content);

        Assert.StartsWith("UNH+", result);
    }

    [Fact]
    public void FormatEdi_EdifactFile_LastSegmentIsUNT()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.FormatEdi(content);
        var lines = result.Split('\n');

        Assert.Equal("UNT+9+1'", lines[^1]);
    }

    [Fact]
    public void FormatEdi_BadFormatFile_SplitsIntoNineSegments()
    {
        // edifactBadFormat.edi has all 9 segments on one line — FormatEdi must split them
        var content = File.ReadAllText(EdifactBadFormatFile);
        var result = EdifactService.FormatEdi(content);
        var lines = result.Split('\n');

        Assert.Equal(9, lines.Length);
    }

    [Fact]
    public void FormatEdi_BadFormatFile_MatchesWellFormattedOutput()
    {
        // Formatting the bad-format file should produce the same output as formatting the well-formatted file
        var good = File.ReadAllText(EdifactFile);
        var bad = File.ReadAllText(EdifactBadFormatFile);

        var goodResult = EdifactService.FormatEdi(good);
        var badResult = EdifactService.FormatEdi(bad);

        Assert.Equal(goodResult, badResult);
    }

    [Fact]
    public void FormatEdi_BadFormatFile_ContainsCorrectSegmentContent()
    {
        var content = File.ReadAllText(EdifactBadFormatFile);
        var result = EdifactService.FormatEdi(content);
        var lines = result.Split('\n');

        for (int i = 0; i < ExpectedSegments.Length; i++)
            Assert.Equal(ExpectedSegments[i], lines[i]);
    }

    // ── MinifyEdi tests ────────────────────────────────────────────────

    [Fact]
    public void MinifyEdi_FormattedEdi_RemovesLineBreaks()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.MinifyEdi(content);
        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("\r", result);
    }

    [Fact]
    public void MinifyEdi_FormattedEdi_PreservesContent()
    {
        var content = File.ReadAllText(EdifactFile);
        var result = EdifactService.MinifyEdi(content);
        Assert.Contains("UNH+1+IFTMCS", result);
        Assert.Contains("UNT+9+1'", result);
    }

    [Fact]
    public void MinifyEdi_MinifiedEdi_IsIdempotent()
    {
        var content = File.ReadAllText(EdifactFile);
        var first = EdifactService.MinifyEdi(content);
        var second = EdifactService.MinifyEdi(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void MinifyEdi_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EdifactService.MinifyEdi(string.Empty));
    }

    [Fact]
    public void MinifyEdi_FormatRoundTrip_PreservesSegments()
    {
        var content = File.ReadAllText(EdifactFile);
        var minified = EdifactService.MinifyEdi(content);
        var formatted = EdifactService.FormatEdi(minified);
        var lines = formatted.Split('\n');
        Assert.Equal(9, lines.Length);
    }

    // ── ValidateEdi tests ──────────────────────────────────────────────

    [Fact]
    public void ValidateEdi_ValidMessage_ReturnsNull()
    {
        Assert.Null(EdifactService.ValidateEdi(SampleEdi));
    }

    [Fact]
    public void ValidateEdi_WellFormattedFile_ReturnsNull()
    {
        var content = File.ReadAllText(EdifactFile);
        Assert.Null(EdifactService.ValidateEdi(content));
    }

    [Fact]
    public void ValidateEdi_BadFormatFile_ReturnsNull()
    {
        var content = File.ReadAllText(EdifactBadFormatFile);
        Assert.Null(EdifactService.ValidateEdi(content));
    }

    [Fact]
    public void ValidateEdi_EmptyString_ReturnsError()
    {
        var error = EdifactService.ValidateEdi(string.Empty);
        Assert.NotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_WhitespaceOnly_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("   \n\t  ");
        Assert.NotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_NoSegmentTerminator_ReturnsErrorWithDescription()
    {
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN");
        Assert.NotNull(error);
        Assert.Contains("terminator", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_InvalidSegmentTag_LowercaseLetters_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("unh+1+ORDERS'");
        Assert.NotNull(error);
        Assert.Contains("uppercase", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_InvalidSegmentTag_SingleChar_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("U+1+ORDERS'");
        Assert.NotNull(error);
        Assert.Contains("too short", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_InvalidSegmentTag_FourChars_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("UNHX+1+ORDERS'");
        Assert.NotNull(error);
        Assert.Contains("too long", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_PlainText_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("This is not EDIFACT content at all.");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateEdi_XmlContent_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("<root><child>value</child></root>");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateEdi_EscapedQuoteOnly_NoRealTerminator_ReturnsError()
    {
        // ?' is an escaped quote — not a segment terminator
        var error = EdifactService.ValidateEdi("TST+value?'more");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateEdi_ErrorMessage_IncludesSegmentNumber()
    {
        // Second segment has lowercase tag
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS'bgm+220+4711'");
        Assert.NotNull(error);
        Assert.Contains("Segment 2", error);
    }

    [Fact]
    public void ValidateEdi_EmptySegment_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS''");
        Assert.NotNull(error);
        Assert.Contains("empty segment", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_TrailingContent_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS'sometrailing");
        Assert.NotNull(error);
        Assert.Contains("Unexpected content", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_UnexpectedCharAfterTag_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("UN1+data'");
        Assert.NotNull(error);
        Assert.Contains("unexpected character", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_LineWithoutTerminator_ReturnsError()
    {
        // Second line is missing the segment terminator
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'\nBGM+220+4711\nUNT+3+1'");
        Assert.NotNull(error);
        Assert.Contains("Line 2", error);
        Assert.Contains("does not end with a segment terminator", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_AllLinesEndWithTerminator_ReturnsNull()
    {
        Assert.Null(EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'\nBGM+220+4711'\nUNT+3+1'"));
    }

    // ── UNH / UNT pairing tests ───────────────────────────────────────

    [Fact]
    public void ValidateEdi_UntCountMismatch_ReturnsError()
    {
        // UNH + BGM + UNT = 3 segments, but UNT declares 5
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'BGM+220+4711'UNT+5+1'");
        Assert.NotNull(error);
        Assert.Contains("actual count", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3", error); // actual count
        Assert.Contains("5", error); // declared count
    }

    [Fact]
    public void ValidateEdi_UntReferenceNumberMismatch_ReturnsError()
    {
        // UNH ref = "1", UNT ref = "99"
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'BGM+220+4711'UNT+3+99'");
        Assert.NotNull(error);
        Assert.Contains("reference number", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("99", error);
        Assert.Contains("1", error);
    }

    [Fact]
    public void ValidateEdi_UntWithoutUnh_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("BGM+220+4711'UNT+2+1'");
        Assert.NotNull(error);
        Assert.Contains("UNT found without a preceding UNH", error);
    }

    [Fact]
    public void ValidateEdi_UnhWithoutUnt_ReturnsError()
    {
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'BGM+220+4711'");
        Assert.NotNull(error);
        Assert.Contains("no matching UNT", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_NestedUnh_ReturnsError()
    {
        // Two UNH segments without a UNT between them
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS'UNH+2+ORDERS'UNT+2+2'");
        Assert.NotNull(error);
        Assert.Contains("second UNH", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEdi_ValidUnhUntPair_ReturnsNull()
    {
        // UNH + BGM + UNT = 3 segments, UNT declares 3, matching ref "1"
        Assert.Null(EdifactService.ValidateEdi("UNH+1+ORDERS:D:96A:UN'BGM+220+4711'UNT+3+1'"));
    }

    [Fact]
    public void ValidateEdi_MultipleValidMessages_ReturnsNull()
    {
        // Two valid UNH/UNT pairs back to back
        const string edi =
            "UNH+1+ORDERS:D:96A:UN'BGM+220'UNT+3+1'" +
            "UNH+2+ORDERS:D:96A:UN'BGM+221'UNT+3+2'";
        Assert.Null(EdifactService.ValidateEdi(edi));
    }

    [Fact]
    public void ValidateEdi_NoUnhOrUnt_ReturnsNull()
    {
        // Messages without UNH/UNT (e.g. just UNB/UNZ envelope) are allowed
        Assert.Null(EdifactService.ValidateEdi("UNB+UNOA:3'UNZ+1+1'"));
    }

    [Fact]
    public void ValidateEdi_MultipleErrors_ReportsAll()
    {
        // UNH ref mismatch AND count mismatch — both should be reported
        var error = EdifactService.ValidateEdi("UNH+1+ORDERS'BGM+220'UNT+99+77'");
        Assert.NotNull(error);
        // Count mismatch: actual 3, declared 99
        Assert.Contains("actual count", error, StringComparison.OrdinalIgnoreCase);
        // Reference mismatch: UNH ref "1", UNT ref "77"
        Assert.Contains("reference number", error, StringComparison.OrdinalIgnoreCase);
        // Both errors present (multi-line)
        var errorLines = error.Split('\n');
        Assert.True(errorLines.Length >= 2, $"Expected at least 2 error lines but got {errorLines.Length}:\n{error}");
    }
}

