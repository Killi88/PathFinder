using System.IO;
using System.Text.Json;

namespace PathFinder.Tests;

public class WindowLayoutSettingsTests : IDisposable
{
    private readonly string _tempFile;

    public WindowLayoutSettingsTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // ── Round-trip via SaveWindowSettings / LoadWindowSettings ───────────

    [Fact]
    public void SaveAndLoad_WithOpenFiles_RoundtripsFileList()
    {
        var files = new List<string> { @"C:\data\invoice.xml", @"C:\data\config.json" };
        var settings = new MainWindow.WindowLayoutSettings(10, 20, 1600, 960, false, 400, files);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Equal(files, loaded!.OpenFiles);
    }

    [Fact]
    public void SaveAndLoad_WithNullOpenFiles_OpenFilesRemainsNull()
    {
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, null);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.OpenFiles);
    }

    [Fact]
    public void SaveAndLoad_WithEmptyOpenFiles_OpenFilesIsEmpty()
    {
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, []);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.OpenFiles);
        Assert.Empty(loaded.OpenFiles!);
    }

    [Fact]
    public void SaveAndLoad_PreservesWindowLayout()
    {
        var settings = new MainWindow.WindowLayoutSettings(100, 200, 1280, 720, true, 350, null);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Equal(100, loaded!.Left);
        Assert.Equal(200, loaded.Top);
        Assert.Equal(1280, loaded.Width);
        Assert.Equal(720, loaded.Height);
        Assert.True(loaded.IsMaximized);
        Assert.Equal(350, loaded.RightPanelWidth);
    }

    // ── LoadWindowSettings edge cases ────────────────────────────────────

    [Fact]
    public void LoadWindowSettings_FileNotFound_ReturnsNull()
    {
        var result = MainWindow.LoadWindowSettings(@"C:\this\does\not\exist.json");

        Assert.Null(result);
    }

    [Fact]
    public void LoadWindowSettings_LegacyJsonWithoutOpenFiles_OpenFilesIsNull()
    {
        // Simulate a settings file saved before the OpenFiles property was added
        const string legacyJson = """
            {"Left":50,"Top":60,"Width":1400,"Height":900,"IsMaximized":false,"RightPanelWidth":380}
            """;
        File.WriteAllText(_tempFile, legacyJson);

        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.OpenFiles);
        Assert.Equal(50, loaded.Left);
        Assert.Equal(380, loaded.RightPanelWidth);
    }

    [Fact]
    public void LoadWindowSettings_InvalidJson_ReturnsNull()
    {
        File.WriteAllText(_tempFile, "not valid json {{{{");

        var result = MainWindow.LoadWindowSettings(_tempFile);

        Assert.Null(result);
    }

    // ── JSON serialisation contract ───────────────────────────────────────

    [Fact]
    public void WindowLayoutSettings_SerializedJson_IncludesOpenFilesProperty()
    {
        var files = new List<string> { @"C:\foo.xml" };
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, files);

        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("OpenFiles", json);
        Assert.Contains(@"C:\\foo.xml", json);
    }

    // ── IsDarkMode persistence ────────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_IsDarkModeTrue_RoundtripsCorrectly()
    {
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, IsDarkMode: true);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsDarkMode);
    }

    [Fact]
    public void SaveAndLoad_IsDarkModeFalse_RoundtripsCorrectly()
    {
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, IsDarkMode: false);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.False(loaded!.IsDarkMode);
    }

    [Fact]
    public void LoadWindowSettings_LegacyJsonWithoutIsDarkMode_DefaultsToTrue()
    {
        const string legacyJson = """
            {"Left":50,"Top":60,"Width":1400,"Height":900,"IsMaximized":false,"RightPanelWidth":380}
            """;
        File.WriteAllText(_tempFile, legacyJson);

        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsDarkMode);
    }

    [Fact]
    public void WindowLayoutSettings_SerializedJson_IncludesIsDarkModeProperty()
    {
        var settings = new MainWindow.WindowLayoutSettings(0, 0, 1600, 960, false, 400, IsDarkMode: false);

        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("IsDarkMode", json);
        Assert.Contains("false", json);
    }

    // ── AllPaths column width persistence ─────────────────────────────────

    [Fact]
    public void SaveAndLoad_AllPathsColumnWidths_RoundtripsCorrectly()
    {
        var settings = new MainWindow.WindowLayoutSettings(
            0, 0, 1600, 960, false, 400,
            AllPathsPathColumnWidth: 320,
            AllPathsValueColumnWidth: 180);

        MainWindow.SaveWindowSettings(settings, _tempFile);
        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Equal(320, loaded!.AllPathsPathColumnWidth);
        Assert.Equal(180, loaded.AllPathsValueColumnWidth);
    }

    [Fact]
    public void LoadWindowSettings_LegacyJsonWithoutColumnWidths_DefaultsTo250And150()
    {
        const string legacyJson = """
            {"Left":50,"Top":60,"Width":1400,"Height":900,"IsMaximized":false,"RightPanelWidth":380}
            """;
        File.WriteAllText(_tempFile, legacyJson);

        var loaded = MainWindow.LoadWindowSettings(_tempFile);

        Assert.NotNull(loaded);
        Assert.Equal(250, loaded!.AllPathsPathColumnWidth);
        Assert.Equal(150, loaded.AllPathsValueColumnWidth);
    }

    [Fact]
    public void WindowLayoutSettings_SerializedJson_IncludesColumnWidthProperties()
    {
        var settings = new MainWindow.WindowLayoutSettings(
            0, 0, 1600, 960, false, 400,
            AllPathsPathColumnWidth: 300,
            AllPathsValueColumnWidth: 200);

        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("AllPathsPathColumnWidth", json);
        Assert.Contains("AllPathsValueColumnWidth", json);
    }
}
