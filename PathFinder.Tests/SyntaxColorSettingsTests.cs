using System.IO;
using PathFinder.Models;

namespace PathFinder.Tests;

public class SyntaxColorSettingsTests
{
    [Fact]
    public void CreateDefault_ContainsAllKeys()
    {
        var settings = SyntaxColorSettings.CreateDefault();
        foreach (var key in SyntaxColorSettings.AllKeys)
        {
            Assert.True(settings.DarkColors.ContainsKey(key), $"DarkColors missing key: {key}");
            Assert.True(settings.LightColors.ContainsKey(key), $"LightColors missing key: {key}");
        }
    }

    [Fact]
    public void CreateDefault_AllValuesAreValidHex()
    {
        var settings = SyntaxColorSettings.CreateDefault();
        foreach (var key in SyntaxColorSettings.AllKeys)
        {
            Assert.True(SyntaxColorSettings.IsValidHex(settings.DarkColors[key]),
                $"Dark color for {key} is not valid hex: {settings.DarkColors[key]}");
            Assert.True(SyntaxColorSettings.IsValidHex(settings.LightColors[key]),
                $"Light color for {key} is not valid hex: {settings.LightColors[key]}");
        }
    }

    [Fact]
    public void GetColor_ReturnsCustomColor_WhenSet()
    {
        var settings = SyntaxColorSettings.CreateDefault();
        settings.DarkColors["XmlComment"] = "#FF0000";

        Assert.Equal("#FF0000", settings.GetColor("XmlComment", dark: true));
    }

    [Fact]
    public void GetColor_ReturnsDefault_WhenKeyMissing()
    {
        var settings = new SyntaxColorSettings();

        var color = settings.GetColor("XmlComment", dark: true);
        Assert.Equal(SyntaxColorSettings.DefaultDarkColors["XmlComment"], color);
    }

    [Fact]
    public void GetColor_ReturnsDefault_WhenInvalidHex()
    {
        var settings = SyntaxColorSettings.CreateDefault();
        settings.DarkColors["XmlComment"] = "not-a-color";

        var color = settings.GetColor("XmlComment", dark: true);
        Assert.Equal(SyntaxColorSettings.DefaultDarkColors["XmlComment"], color);
    }

    [Fact]
    public void GetColor_DarkAndLight_ReturnDifferentDefaults()
    {
        var settings = SyntaxColorSettings.CreateDefault();

        var dark = settings.GetColor("XmlComment", dark: true);
        var light = settings.GetColor("XmlComment", dark: false);

        Assert.NotEqual(dark, light);
    }

    [Theory]
    [InlineData("#AABBCC", true)]
    [InlineData("#aabbcc", true)]
    [InlineData("#000000", true)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#12AB9f", true)]
    [InlineData("AABBCC", false)]
    [InlineData("#AABBCG", false)]
    [InlineData("#AABBC", false)]
    [InlineData("#AABBCCD", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("red", false)]
    public void IsValidHex_ValidatesCorrectly(string? hex, bool expected)
    {
        Assert.Equal(expected, SyntaxColorSettings.IsValidHex(hex));
    }

    [Fact]
    public void SaveAndLoad_RoundtripsCustomColors()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pathfinder_test_{Guid.NewGuid()}.json");
        try
        {
            var settings = SyntaxColorSettings.CreateDefault();
            settings.DarkColors["XmlComment"] = "#FF0000";
            settings.LightColors["JsonKey"] = "#00FF00";

            SyntaxColorSettings.Save(settings, tempPath);
            var loaded = SyntaxColorSettings.Load(tempPath);

            Assert.Equal("#FF0000", loaded.GetColor("XmlComment", dark: true));
            Assert.Equal("#00FF00", loaded.GetColor("JsonKey", dark: false));
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void Load_FileNotFound_ReturnsDefault()
    {
        var result = SyntaxColorSettings.Load("nonexistent_file_12345.json");

        Assert.NotNull(result);
        Assert.Equal(SyntaxColorSettings.DefaultDarkColors["XmlComment"],
            result.GetColor("XmlComment", dark: true));
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefault()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pathfinder_test_{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempPath, "not valid json {{{");
            var result = SyntaxColorSettings.Load(tempPath);

            Assert.NotNull(result);
            Assert.Equal(SyntaxColorSettings.DefaultDarkColors["XmlComment"],
                result.GetColor("XmlComment", dark: true));
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void SaveAndLoad_PreservesAllDefaultColors()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pathfinder_test_{Guid.NewGuid()}.json");
        try
        {
            var original = SyntaxColorSettings.CreateDefault();
            SyntaxColorSettings.Save(original, tempPath);
            var loaded = SyntaxColorSettings.Load(tempPath);

            foreach (var key in SyntaxColorSettings.AllKeys)
            {
                Assert.Equal(original.GetColor(key, true), loaded.GetColor(key, true));
                Assert.Equal(original.GetColor(key, false), loaded.GetColor(key, false));
            }
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void AllKeys_ContainsXmlJsonAndEdiKeys()
    {
        Assert.Equal(
            SyntaxColorSettings.XmlKeys.Length +
            SyntaxColorSettings.JsonKeys.Length +
            SyntaxColorSettings.EdiKeys.Length +
            SyntaxColorSettings.YamlKeys.Length,
            SyntaxColorSettings.AllKeys.Length);
    }

    [Fact]
    public void DisplayNames_HasEntryForEveryKey()
    {
        foreach (var key in SyntaxColorSettings.AllKeys)
            Assert.True(SyntaxColorSettings.DisplayNames.ContainsKey(key),
                $"DisplayNames missing key: {key}");
    }

    [Fact]
    public void DefaultDarkColors_MatchExpectedXmlCommentColor()
    {
        Assert.Equal("#6A9955", SyntaxColorSettings.DefaultDarkColors["XmlComment"]);
    }

    [Fact]
    public void DefaultLightColors_MatchExpectedXmlCommentColor()
    {
        Assert.Equal("#3A7A1E", SyntaxColorSettings.DefaultLightColors["XmlComment"]);
    }

    [Fact]
    public void GetColor_LightTheme_ReturnsLightColor()
    {
        var settings = SyntaxColorSettings.CreateDefault();
        settings.LightColors["XmlTagName"] = "#ABCDEF";

        Assert.Equal("#ABCDEF", settings.GetColor("XmlTagName", dark: false));
        // Dark should still be the default
        Assert.Equal(SyntaxColorSettings.DefaultDarkColors["XmlTagName"],
            settings.GetColor("XmlTagName", dark: true));
    }
}
