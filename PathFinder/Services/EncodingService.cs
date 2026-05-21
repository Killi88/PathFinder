using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PathFinder.Services;

/// <summary>
/// Represents one of the encoding options shown in the status-bar encoding picker.
/// </summary>
public sealed class EncodingOption
{
    public string DisplayName { get; }
    public Encoding Encoding { get; }
    /// <summary>Value to write into the XML declaration's encoding attribute.</summary>
    public string XmlEncodingName { get; }

    public EncodingOption(string displayName, Encoding encoding, string xmlEncodingName)
    {
        DisplayName = displayName;
        Encoding = encoding;
        XmlEncodingName = xmlEncodingName;
    }

    public override string ToString() => DisplayName;
}

/// <summary>
/// Handles file encoding detection, reading, writing, and XML declaration updates.
/// </summary>
public static class EncodingService
{
    // Matches the encoding value inside an XML declaration, e.g. encoding="utf-16" or encoding='utf-16'
    // Groups: [1] = everything up to and including the opening quote, [2] = current encoding name
    // Handles both quote styles and optional whitespace around '='
    private static readonly Regex XmlEncodingRegex =
        new(@"(<\?xml\b[^?]*?\bencoding\s*=\s*[""'])([\w-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>All encodings shown in the status-bar picker, in display order.</summary>
    public static IReadOnlyList<EncodingOption> SupportedEncodings { get; }

    static EncodingService()
    {
        SupportedEncodings =
        [
            new("UTF-8",         new UTF8Encoding(false),          "utf-8"),
            new("UTF-8 BOM",     new UTF8Encoding(true),           "utf-8"),
            new("UTF-16 LE BOM", new UnicodeEncoding(false, true), "utf-16"),
            new("UTF-16 BE BOM", new UnicodeEncoding(true,  true), "utf-16"),
        ];
    }

    /// <summary>
    /// Reads a file, detects its encoding from the BOM (or defaults to UTF-8),
    /// and returns the decoded text together with the matched <see cref="EncodingOption"/>.
    /// For files without a BOM the XML encoding declaration is used as a second hint.
    /// </summary>
    public static (string text, EncodingOption encoding) ReadFileWithEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var option = DetectEncoding(bytes, out int bomLength);

        // If no BOM was found, try the XML encoding declaration as a second hint
        if (bomLength == 0)
        {
            var declaredOption = DetectEncodingFromXmlDeclaration(bytes);
            if (declaredOption != null)
                option = declaredOption;
        }

        var text = option.Encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        return (text, option);
    }

    /// <summary>
    /// Attempts to read an encoding name from an XML declaration in <paramref name="bytes"/>
    /// and map it to a <see cref="SupportedEncodings"/> entry that does not require a BOM.
    /// Returns <see langword="null"/> when no declaration is present or no matching encoding is found.
    /// </summary>
    private static EncodingOption? DetectEncodingFromXmlDeclaration(byte[] bytes)
    {
        // XML declarations consist entirely of ASCII characters, so reading the header as ASCII is safe
        var headerLength = Math.Min(bytes.Length, 200);
        var header = Encoding.ASCII.GetString(bytes, 0, headerLength);
        var match = XmlEncodingRegex.Match(header);
        if (!match.Success)
            return null;

        var declaredName = match.Groups[2].Value;
        // Only accept encodings that don't require a BOM — we already know there is no BOM
        return SupportedEncodings.FirstOrDefault(e =>
            string.Equals(e.XmlEncodingName, declaredName, StringComparison.OrdinalIgnoreCase)
            && !e.DisplayName.EndsWith("BOM", StringComparison.Ordinal));
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="filePath"/> using
    /// <paramref name="option"/>'s encoding. If the text contains an XML declaration
    /// with an encoding attribute, that attribute value is updated to match before writing.
    /// </summary>
    public static void WriteFileWithEncoding(string filePath, string text, EncodingOption option)
    {
        var toWrite = UpdateXmlDeclarationEncoding(text, option.XmlEncodingName);
        File.WriteAllText(filePath, toWrite, option.Encoding);
    }

    /// <summary>
    /// Replaces the encoding attribute value inside an XML declaration
    /// (e.g. <c>encoding="utf-16"</c> → <c>encoding="utf-8"</c>).
    /// Returns the text unchanged when no XML declaration with an encoding attribute is found.
    /// </summary>
    public static string UpdateXmlDeclarationEncoding(string xmlText, string newEncodingName)
    {
        return XmlEncodingRegex.Replace(xmlText,
            m => m.Groups[1].Value + newEncodingName,
            count: 1);
    }

    /// <summary>
    /// Detects the encoding by reading the BOM at the start of <paramref name="bytes"/>.
    /// Sets <paramref name="bomLength"/> to the number of BOM bytes to skip when decoding.
    /// Falls back to UTF-8 when no recognised BOM is found.
    /// </summary>
    public static EncodingOption DetectEncoding(byte[] bytes, out int bomLength)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            bomLength = 2;
            return SupportedEncodings.First(e => e.DisplayName == "UTF-16 LE BOM");
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            bomLength = 2;
            return SupportedEncodings.First(e => e.DisplayName == "UTF-16 BE BOM");
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bomLength = 3;
            return SupportedEncodings.First(e => e.DisplayName == "UTF-8 BOM");
        }
        bomLength = 0;
        return SupportedEncodings.First(e => e.DisplayName == "UTF-8");
    }

}
