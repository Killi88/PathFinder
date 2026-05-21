using System.IO;
using System.Linq;
using System.Text;
using PathFinder.Services;

namespace PathFinder.Tests;

public class EncodingServiceTests
{
    // ── DetectEncoding from bytes ─────────────────────────────────────────

    [Fact]
    public void DetectEncoding_Utf16LeBom_ReturnsUtf16Le()
    {
        // FF FE BOM followed by "hi" in UTF-16 LE
        var bytes = new byte[] { 0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00 };
        var option = EncodingService.DetectEncoding(bytes, out int bomLength);
        Assert.Equal("UTF-16 LE BOM", option.DisplayName);
        Assert.Equal(2, bomLength);
    }

    [Fact]
    public void DetectEncoding_Utf16BeBom_ReturnsUtf16Be()
    {
        // FE FF BOM followed by "hi" in UTF-16 BE
        var bytes = new byte[] { 0xFE, 0xFF, 0x00, 0x68, 0x00, 0x69 };
        var option = EncodingService.DetectEncoding(bytes, out int bomLength);
        Assert.Equal("UTF-16 BE BOM", option.DisplayName);
        Assert.Equal(2, bomLength);
    }

    [Fact]
    public void DetectEncoding_Utf8Bom_ReturnsUtf8BomWithBomLength3()
    {
        // EF BB BF BOM followed by "hi"
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x68, 0x69 };
        var option = EncodingService.DetectEncoding(bytes, out int bomLength);
        Assert.Equal("UTF-8 BOM", option.DisplayName);
        Assert.Equal(3, bomLength);
    }

    [Fact]
    public void DetectEncoding_NoBom_ReturnsUtf8WithZeroBomLength()
    {
        var bytes = Encoding.UTF8.GetBytes("Hello, world!");
        var option = EncodingService.DetectEncoding(bytes, out int bomLength);
        Assert.Equal("UTF-8", option.DisplayName);
        Assert.Equal(0, bomLength);
    }

    [Fact]
    public void DetectEncoding_EmptyBytes_ReturnsUtf8()
    {
        var option = EncodingService.DetectEncoding([], out int bomLength);
        Assert.Equal("UTF-8", option.DisplayName);
        Assert.Equal(0, bomLength);
    }

    // ── UpdateXmlDeclarationEncoding ──────────────────────────────────────

    [Fact]
    public void UpdateXmlDeclarationEncoding_WithDoubleQuotes_UpdatesCorrectly()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Contains("encoding=\"utf-8\"", result);
        Assert.DoesNotContain("encoding=\"utf-16\"", result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_NoDeclaration_ReturnsUnchanged()
    {
        const string xml = "<root><item/></root>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Equal(xml, result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_AlreadyCorrectEncoding_ReturnsEquivalent()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Equal(xml, result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_CaseInsensitiveMatch()
    {
        const string xml = "<?xml version=\"1.0\" ENCODING=\"UTF-16\"?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Contains("utf-8", result);
        Assert.DoesNotContain("UTF-16", result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_OnlyUpdatesFirstOccurrence()
    {
        // A second encoding="..." in content must not be touched
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<root encoding=\"utf-16\"/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
        Assert.Contains("encoding=\"utf-16\"", result); // second occurrence unchanged
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_WithSingleQuotes_UpdatesCorrectly()
    {
        const string xml = "<?xml version='1.0' encoding='utf-16'?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Contains("encoding='utf-8'", result);
        Assert.DoesNotContain("encoding='utf-16'", result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_WithSingleQuotes_PreservesQuoteStyle()
    {
        const string xml = "<?xml version='1.0' encoding='utf-16'?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        // Must keep single quotes, not switch to double quotes
        Assert.Contains("encoding='utf-8'", result);
        Assert.DoesNotContain("encoding=\"utf-8\"", result);
    }

    [Fact]
    public void UpdateXmlDeclarationEncoding_WhitespaceAroundEquals_UpdatesCorrectly()
    {
        const string xml = "<?xml version=\"1.0\" encoding = \"utf-16\" ?>\n<root/>";
        var result = EncodingService.UpdateXmlDeclarationEncoding(xml, "utf-8");
        Assert.Contains("utf-8", result);
        Assert.DoesNotContain("utf-16", result);
    }

    // ── WriteFileWithEncoding / ReadFileWithEncoding round-trips ─────────

    [Fact]
    public void WriteAndRead_Utf8WithBom_WritesWithBomAndRoundtrips()
    {
        var path = Path.GetTempFileName();
        try
        {
            var option = GetOption("UTF-8 BOM");
            EncodingService.WriteFileWithEncoding(path, "Hello UTF-8 BOM!", option);

            // Verify EF BB BF BOM is present
            var raw = File.ReadAllBytes(path);
            Assert.Equal(0xEF, raw[0]);
            Assert.Equal(0xBB, raw[1]);
            Assert.Equal(0xBF, raw[2]);

            var (text, detected) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("Hello UTF-8 BOM!", text);
            Assert.Equal("UTF-8 BOM", detected.DisplayName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WriteAndRead_Utf8NoBom_WritesNoBom()
    {
        var path = Path.GetTempFileName();
        try
        {
            EncodingService.WriteFileWithEncoding(path, "Hello UTF-8!", GetOption("UTF-8"));
            var raw = File.ReadAllBytes(path);
            // Must NOT start with EF BB BF
            Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WriteAndRead_Utf8_RoundtripsText()
    {
        var path = Path.GetTempFileName();
        try
        {
            var option = GetOption("UTF-8");
            EncodingService.WriteFileWithEncoding(path, "Hello UTF-8!", option);
            var (text, detected) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("Hello UTF-8!", text);
            Assert.Equal("UTF-8", detected.DisplayName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WriteAndRead_Utf16Le_WritesWithBomAndRoundtrips()
    {
        var path = Path.GetTempFileName();
        try
        {
            var option = GetOption("UTF-16 LE BOM");
            EncodingService.WriteFileWithEncoding(path, "Hello UTF-16!", option);

            // Verify BOM is present
            var raw = File.ReadAllBytes(path);
            Assert.Equal(0xFF, raw[0]);
            Assert.Equal(0xFE, raw[1]);

            var (text, detected) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("Hello UTF-16!", text);
            Assert.Equal("UTF-16 LE BOM", detected.DisplayName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WriteAndRead_Utf16Be_WritesWithBomAndRoundtrips()
    {
        var path = Path.GetTempFileName();
        try
        {
            var option = GetOption("UTF-16 BE BOM");
            EncodingService.WriteFileWithEncoding(path, "Hello UTF-16 BE!", option);

            var raw = File.ReadAllBytes(path);
            Assert.Equal(0xFE, raw[0]);
            Assert.Equal(0xFF, raw[1]);

            var (text, detected) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("Hello UTF-16 BE!", text);
            Assert.Equal("UTF-16 BE BOM", detected.DisplayName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WriteFileWithEncoding_Xml_UpdatesDeclarationInFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<root/>";
            EncodingService.WriteFileWithEncoding(path, xml, GetOption("UTF-8"));
            var written = File.ReadAllText(path, Encoding.UTF8);
            Assert.Contains("encoding=\"utf-8\"", written);
            Assert.DoesNotContain("encoding=\"utf-16\"", written);
        }
        finally { File.Delete(path); }
    }

    // ── SupportedEncodings list ───────────────────────────────────────────

    [Fact]
    public void SupportedEncodings_ContainsAllRequiredOptions()
    {
        var names = EncodingService.SupportedEncodings.Select(e => e.DisplayName).ToList();
        Assert.Contains("UTF-8", names);
        Assert.Contains("UTF-8 BOM", names);
        Assert.Contains("UTF-16 LE BOM", names);
        Assert.Contains("UTF-16 BE BOM", names);
        Assert.DoesNotContain("ANSI / Windows-1252", names);
        Assert.DoesNotContain("ISO-8859-1", names);
    }

    [Fact]
    public void SupportedEncodings_HasCorrectXmlEncodingNames()
    {
        Assert.Equal("utf-8", GetOption("UTF-8").XmlEncodingName);
        Assert.Equal("utf-8", GetOption("UTF-8 BOM").XmlEncodingName);
        Assert.Equal("utf-16", GetOption("UTF-16 LE BOM").XmlEncodingName);
        Assert.Equal("utf-16", GetOption("UTF-16 BE BOM").XmlEncodingName);
    }

    // ── ReadFileWithEncoding — XML declaration hint ─────────────────────────

    [Fact]
    public void ReadFileWithEncoding_XmlDeclarationUtf8_NoBom_DetectsUtf8()
    {
        var path = Path.GetTempFileName();
        try
        {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root/>";
            File.WriteAllText(path, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var (text, encoding) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("UTF-8", encoding.DisplayName);
            Assert.Contains("<root", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReadFileWithEncoding_XmlDeclarationUtf16_NoBom_FallsBackToUtf8()
    {
        // A file saved as UTF-8 but incorrectly declaring utf-16 has no BOM,
        // so the utf-16 declaration must be ignored and UTF-8 returned.
        var path = Path.GetTempFileName();
        try
        {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<root/>";
            File.WriteAllText(path, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var (text, encoding) = EncodingService.ReadFileWithEncoding(path);
            Assert.Equal("UTF-8", encoding.DisplayName);
            Assert.Contains("<root", text);
        }
        finally { File.Delete(path); }
    }

    // ── ReadFileWithEncoding against real test files ───────────────────────

    [Fact]
    public void ReadFileWithEncoding_XPathHighlightXml_DetectsUtf8NoBom()
    {
        // XPathHighlight.xml has no BOM — the XML declaration says utf-16 but
        // the file was saved without a BOM, so detection falls back to UTF-8.
        var path = TestFilePath("XPathHighlight.xml");
        var (text, encoding) = EncodingService.ReadFileWithEncoding(path);
        Assert.Equal("UTF-8", encoding.DisplayName);
        Assert.Contains("<xml", text);
    }

    [Fact]
    public void ReadFileWithEncoding_ExampleJson_DetectsUtf8()
    {
        var path = TestFilePath("example.json");
        var (text, encoding) = EncodingService.ReadFileWithEncoding(path);
        Assert.Equal("UTF-8", encoding.DisplayName);
        Assert.Contains("MAERSK", text);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static EncodingOption GetOption(string name) =>
        EncodingService.SupportedEncodings.First(e => e.DisplayName == name);

    private static string TestFilePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", filename);
}
