using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig;

namespace PathFinder;

public partial class HelpWindow : Window
{
    public HelpWindow(bool isDark)
    {
        InitializeComponent();
        ApplyTheme(isDark);
        LoadManual();
    }

    private void LoadManual()
    {
        var markdown = ReadEmbeddedManual();
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        var bodyHtml = Markdig.Markdown.ToHtml(markdown, pipeline);
        var fullHtml = WrapInHtml(bodyHtml);
        helpBrowser.NavigateToString(fullHtml);
    }

    private static string ReadEmbeddedManual()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("MANUAL.md", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string WrapInHtml(string bodyHtml)
    {
        string Hex(string key) => ColorToHex((Color)FindResource(key));
        string BrushHex(string key) => ColorToHex(((SolidColorBrush)FindResource(key)).Color);

        var bg = Hex("HelpHtmlBackground");
        var fg = Hex("HelpHtmlForeground");
        var h1Color = Hex("HelpHtmlH1");
        var h2Color = Hex("HelpHtmlH2");
        var h3Color = Hex("HelpHtmlH3");
        var codeColor = Hex("HelpHtmlCode");
        var codeBg = Hex("HelpHtmlCodeBackground");
        var linkColor = Hex("HelpHtmlLink");
        var hrColor = Hex("HelpHtmlHr");
        var tableBorder = Hex("HelpHtmlTableBorder");
        var thBg = Hex("HelpHtmlTableHeader");
        var scrollTrack = BrushHex("ScrollBarTrack");
        var scrollThumb = BrushHex("ScrollBarThumb");
        var scrollThumbHover = BrushHex("ScrollBarThumbHover");

        return $@"<html>
<head>
<meta charset=""utf-8""/>
<style>
  body {{
    background: {bg}; color: {fg};
    font-family: 'Segoe UI', sans-serif; font-size: 13px;
    line-height: 1.6; margin: 0; padding: 24px 28px 16px 28px;
    scrollbar-face-color: {scrollThumb};
    scrollbar-track-color: {scrollTrack};
    scrollbar-arrow-color: {scrollThumbHover};
    scrollbar-shadow-color: {scrollTrack};
    scrollbar-3dlight-color: {scrollTrack};
    scrollbar-darkshadow-color: {scrollTrack};
    scrollbar-highlight-color: {scrollTrack};
  }}
  h1 {{ color: {h1Color}; font-size: 22px; font-weight: 600; margin-top: 0; }}
  h2 {{ color: {h2Color}; font-size: 15px; font-weight: 600; margin-top: 24px; margin-bottom: 6px; }}
  h3 {{ color: {h3Color}; font-size: 12px; font-weight: 600; margin-top: 14px; margin-bottom: 4px; }}
  p {{ margin: 0 0 8px 0; }}
  ul {{ margin: 4px 0 8px 0; padding-left: 24px; }}
  li {{ margin-bottom: 3px; }}
  code {{
    font-family: Consolas, 'Courier New', monospace; font-size: 12px;
    color: {codeColor}; background: {codeBg};
    padding: 1px 5px; border-radius: 3px;
  }}
  pre {{ background: {codeBg}; padding: 10px; border-radius: 4px; overflow-x: auto; }}
  pre code {{ padding: 0; background: none; }}
  a {{ color: {linkColor}; text-decoration: none; }}
  hr {{ border: none; height: 1px; background: {hrColor}; margin: 16px 0; }}
  strong {{ font-weight: 600; }}
  table {{
    border-collapse: collapse; margin: 8px 0; width: 100%;
  }}
  th, td {{
    border: 1px solid {tableBorder}; padding: 6px 12px;
    text-align: left; font-size: 12px;
  }}
  th {{ background: {thBg}; font-weight: 600; }}
</style>
</head>
<body>{bodyHtml}</body>
</html>";
    }

    private void ApplyTheme(bool dark)
    {
        var themeFile = dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        var themeUri = new Uri(themeFile, UriKind.Relative);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

        footerBorder.SetResourceReference(Border.BackgroundProperty, "HelpFooterBackground");
        footerBorder.SetResourceReference(Border.BorderBrushProperty, "HelpFooterBorder");
        footerBorder.BorderThickness = new Thickness(0, 1, 0, 0);
        footerText.SetResourceReference(TextBlock.ForegroundProperty, "HelpFooterForeground");
    }

    private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
