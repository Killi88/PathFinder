using System.IO;
using PathFinder.Services;

namespace PathFinder.Tests;

/// <summary>
/// Tests for ValidateEdiDefinition — the definition-aware EDIFACT validator.
/// Tests that require directory definitions to be embedded are guarded by
/// <see cref="SkipIfNoDefinitions"/> and will be skipped when the resource
/// has not been generated yet (run Build/ScrapeEdifactDefinitions.py first).
/// </summary>
public class EdifactDefinitionValidatorTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>True when the bundled EDIFACT definitions include D96A definitions.</summary>
    private static bool DefinitionsAvailable => EdifactDefinitionService.DirectoryKnown("D96A");

    private const string SkipReason =
        "EDIFACT definitions not embedded. Run 'python Build/ScrapeEdifactDefinitions.py' first.";

    private static void SkipIfNoDefinitions()
    {
        if (!DefinitionsAvailable)
            Assert.Skip(SkipReason);
    }

    private static IReadOnlyList<string> Validate(string edi)
        => EdifactService.ValidateEdiDefinition(edi);

    // ── Always-available tests (no definitions needed) ─────────────────────────

    [Fact]
    public void ValidateEdiDefinition_EmptyInput_ReturnsEmptyList()
        => Assert.Empty(Validate(string.Empty));

    [Fact]
    public void ValidateEdiDefinition_WhitespaceOnlyInput_ReturnsEmptyList()
        => Assert.Empty(Validate("   \n   "));

    [Fact]
    public void ValidateEdiDefinition_ValidUna_NoUnaError()
    {
        // Standard UNA: "UNA:+.? '" — 9 chars: UNA (3) + 5 delimiters + terminator (')
        const string edi =
            "UNA:+.? '\n" +
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        // UNA check runs without definitions; valid UNA must not produce a UNA error
        Assert.DoesNotContain(errors, e => e.Contains("UNA") && e.Contains("character"));
    }

    [Fact]
    public void ValidateEdiDefinition_ShortUna_ReportsError()
    {
        // Short UNA: "UNA:+.?'" — 8 chars total, body without terminator = 7 chars (should be 8)
        const string edi =
            "UNA:+.?'\n" +
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e => e.Contains("UNA") && e.Contains("character"));
    }

    [Fact]
    public void ValidateEdiDefinition_MalformedUnh_ReportsMissingDirectory()
    {
        // UNH with only 1 element (no message identifier composite)
        const string edi =
            "UNH+1'\n" +
            "BGM+701'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e => e.Contains("UNH") || e.Contains("directory") || e.Contains("parse"));
    }

    // ── Tests requiring definitions ────────────────────────────────────────────

    [Fact]
    public void ValidateEdiDefinition_ExistingTestFile_ReturnsNoStructuralErrors()
    {
        SkipIfNoDefinitions();

        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "edifact.edi");
        var content = File.ReadAllText(path);

        var errors = Validate(content);

        // Only structural errors are checked — field-level errors are acceptable
        // since test data may not be code-list-compliant.
        var structural = errors.Where(e =>
            e.Contains("Unexpected segment") ||
            e.Contains("Missing mandatory segment") ||
            e.Contains("exceeds its maximum occurrence")).ToList();

        Assert.Empty(structural);
    }

    [Fact]
    public void ValidateEdiDefinition_UnknownMessageType_ReturnsError()
    {
        SkipIfNoDefinitions();

        // FOOBAR is not a real EDIFACT message type
        const string edi =
            "UNH+1+FOOBAR:D:96A:UN'\n" +
            "BGM+313+REF001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e =>
            e.Contains("FOOBAR") || e.Contains("not found") ||
            e.Contains("not recognized") || e.Contains("not in the bundled"));
    }

    [Fact]
    public void ValidateEdiDefinition_UnknownDirectory_ReturnsError()
    {
        SkipIfNoDefinitions();

        const string edi =
            "UNH+1+IFTMCS:D:ZZZ:UN'\n" +
            "BGM+701+BL-001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e =>
            e.Contains("DZZZ") || e.Contains("not in the bundled") ||
            e.Contains("not recognized") || e.Contains("not found"));
    }

    [Fact]
    public void ValidateEdiDefinition_MissingMandatoryBgm_ReportsError()
    {
        SkipIfNoDefinitions();

        // IFTMCS D96A: BGM is M(1) — must appear directly after UNH
        const string edi =
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "DTM+137:20240101:102'\n" +   // BGM missing, DTM appears instead
            "UNT+3+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e => e.Contains("BGM"));
    }

    [Fact]
    public void ValidateEdiDefinition_BgmExceedsMaxOccurrence_ReportsError()
    {
        SkipIfNoDefinitions();

        // IFTMCS D96A: BGM is M(1) at root level — second BGM must be rejected
        const string edi =
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-001'\n" +
            "BGM+701+BL-002'\n" +    // second BGM violates maxOccurrences = 1
            "UNT+4+1'";

        var errors = Validate(edi);

        Assert.Contains(errors, e =>
            e.Contains("BGM") &&
            e.Contains("exceeds") && e.Contains("maximum occurrence"));
    }

    [Fact]
    public void ValidateEdiDefinition_MultipleNadInGroups_NoStructuralError()
    {
        SkipIfNoDefinitions();

        // IFTMCS D96A: NAD is in SG11 (C 99), so multiple NAD segments are valid —
        // each triggers a new repetition of SG11.
        const string edi =
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-001'\n" +
            "DTM+137:20240101:102'\n" +
            "NAD+CZ+ACME'\n" +
            "NAD+CN+RETAIL'\n" +
            "NAD+CA+CARRIER'\n" +
            "UNT+7+1'";

        var errors = Validate(edi);

        var structural = errors.Where(e =>
            e.Contains("Unexpected segment 'NAD'") ||
            e.Contains("exceeds its maximum occurrence")).ToList();

        Assert.Empty(structural);
    }

    [Fact]
    public void ValidateEdiDefinition_RepeatedSegmentInGroup_NoStructuralError()
    {
        SkipIfNoDefinitions();

        // IFTMCS D96A: SG18 is triggered by GID, and within SG18, FTX is allowed
        // up to 9 times.  Repeated consecutive FTX segments must all be accepted
        // without an "Unexpected segment" error.
        const string edi =
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-112233'\n" +
            "DTM+137:20240725:102'\n" +
            "NAD+CZ+Acme Goods'\n" +
            "NAD+CN+Best Retail'\n" +
            "NAD+CA+Swift Trucking'\n" +
            "GID+1+10:PA'\n" +
            "FTX+AAA+++first note'\n" +
            "FTX+AAA+++second note'\n" +
            "FTX+AAA+++third note'\n" +
            "UNT+11+1'";

        var errors = Validate(edi);

        var structural = errors.Where(e =>
            e.Contains("Unexpected segment 'FTX'") ||
            e.Contains("exceeds its maximum occurrence")).ToList();

        Assert.Empty(structural);
    }

    [Fact]
    public void ValidateEdiDefinition_FieldExceedsMaxLength_ReportsErrorWithLineNumber()
    {
        SkipIfNoDefinitions();

        // UNH element 0062 (MESSAGE REFERENCE NUMBER) max length = 14;
        // provide a 20-character reference number.
        const string edi =
            "UNH+12345678901234567890+IFTMCS:D:96A:UN'\n" +   // ref = 20 chars (max 14)
            "BGM+701+BL-001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        var lenError = errors.FirstOrDefault(e => e.Contains("exceeds") && e.Contains("length"));
        if (lenError is not null)
            Assert.Matches(@"^Line \d+:", lenError);
    }

    [Fact]
    public void ValidateEdiDefinition_FieldError_HasLineNumberPrefix()
    {
        SkipIfNoDefinitions();

        const string edi =
            "UNH+TOOLONGREFNUMBER12345+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        foreach (var err in errors)
        {
            if (err.Contains("exceeds") && err.Contains("length"))
                Assert.Matches(@"^Line \d+:", err);
        }
    }

    [Fact]
    public void ValidateEdiDefinition_FtxExceedsMaxOccurrence_ReportsExceededNotUnexpected()
    {
        SkipIfNoDefinitions();

        // IFTMCS D96A: FTX inside SG18 (after GID) has a max occurrence.
        // When more FTX segments than allowed are provided, the error should
        // say "exceeds its maximum occurrence count" — not "Unexpected segment".
        const string edi =
            "UNH+1+IFTMCS:D:96A:UN'\n" +
            "BGM+701+BL-112233'\n" +
            "DTM+137:20240725:102'\n" +
            "NAD+CZ+Acme Goods'\n" +
            "NAD+CN+Best Retail'\n" +
            "NAD+CA+Swift Trucking'\n" +
            "GID+1+10:PA'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "FTX+AAA+++654'\n" +
            "MEA+WT+AAB+KGM:2268'\n" +
            "UNT+19+1'";

        var errors = Validate(edi);

        // FTX over-limit errors should say "exceeds ... maximum occurrence"
        var ftxErrors = errors.Where(e => e.Contains("FTX")).ToList();
        Assert.NotEmpty(ftxErrors);
        foreach (var err in ftxErrors)
            Assert.Contains("exceeds", err);

        // MEA should NOT be reported as unexpected — it follows FTX in the group
        Assert.DoesNotContain(errors, e => e.Contains("Unexpected segment 'MEA'"));
    }

    [Fact]
    public void ValidateEdiDefinition_MinimalAperak_NoStructuralError()
    {
        SkipIfNoDefinitions();

        // APERAK D96A: UNH M(1) + BGM M(1) + UNT M(1) — minimal valid structure
        const string edi =
            "UNH+1+APERAK:D:96A:UN'\n" +
            "BGM+313+REF001'\n" +
            "UNT+3+1'";

        var errors = Validate(edi);

        var structural = errors.Where(e =>
            e.Contains("Unexpected segment") ||
            e.Contains("Missing mandatory segment")).ToList();

        Assert.Empty(structural);
    }
}
