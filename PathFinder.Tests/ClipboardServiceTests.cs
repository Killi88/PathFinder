using PathFinder.Services;
using System.IO;

namespace PathFinder.Tests;

public class ClipboardServiceTests
{
    private const string SimpleJson =
        "{\n" +
        "    \"name\": \"Alice\",\n" +
        "    \"age\": 30,\n" +
        "    \"active\": true,\n" +
        "    \"score\": null\n" +
        "}";

    [Fact]
    public void BuildJsonHtml_Key_UsesKeyColor()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        // Keys must be colored #2E75B6 (deep blue)
        Assert.Contains("color:#2E75B6", html);
    }

    [Fact]
    public void BuildJsonHtml_StringValue_UsesStringColor()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        // String values must be colored #A31515 (dark red)
        Assert.Contains("color:#A31515", html);
    }

    [Fact]
    public void BuildJsonHtml_Key_IsDistinctFromStringValue()
    {
        // A line with both a key and a string value should have BOTH colors
        const string json = "{\"key\": \"value\"}";
        var html = ClipboardService.BuildJsonHtml(json);
        Assert.Contains("color:#2E75B6", html);  // key color
        Assert.Contains("color:#A31515", html);  // string value color
    }

    [Fact]
    public void BuildJsonHtml_Number_UsesPrimitiveColor()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        Assert.Contains("color:#098658", html);
    }

    [Fact]
    public void BuildJsonHtml_Boolean_UsesPrimitiveColor()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        Assert.Contains("color:#098658", html);
    }

    [Fact]
    public void BuildJsonHtml_Null_UsesPrimitiveColor()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        Assert.Contains("color:#098658", html);
    }

    [Fact]
    public void BuildJsonHtml_UsesCascadiaMonoFont()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        Assert.Contains("Cascadia Mono", html);
    }

    [Fact]
    public void BuildJsonHtml_HasFontSize9pt5()
    {
        var html = ClipboardService.BuildJsonHtml(SimpleJson);
        Assert.Contains("font-size:9.5pt", html);
    }

    [Fact]
    public void BuildJsonHtml_ExampleJson_KeysAreBlueNotRed()
    {
        // Use the real example.json content (formatted with 4-space indent like PathFinder produces)
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestFiles", "example.json"));
        string formatted = JsonService.FormatJson(json);
        var html = ClipboardService.BuildJsonHtml(formatted);

        // "TargetDeserializationType" must be wrapped in key color (blue #2E75B6)
        Assert.Contains("<span style=\"color:#2E75B6\">&quot;TargetDeserializationType&quot;</span>", html);

        // "ShippingInstructionsNotification" must be wrapped in string color (red #A31515)
        Assert.Contains("<span style=\"color:#A31515\">&quot;ShippingInstructionsNotification&quot;</span>", html);
    }
}
