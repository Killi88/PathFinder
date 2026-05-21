using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PathFinder.Models;

namespace PathFinder;

public partial class SyntaxColorsWindow : Window
{
    private readonly SyntaxColorSettings _settings;
    private readonly Dictionary<string, TextBox> _hexBoxes = new();
    private readonly Dictionary<string, Rectangle> _swatches = new();
    private bool _editingDark = true;

    internal SyntaxColorSettings? Result { get; private set; }
    private Action<SyntaxColorSettings>? _onApply;

    internal SyntaxColorsWindow(bool isDark, SyntaxColorSettings settings, Action<SyntaxColorSettings>? onApply = null)
    {
        InitializeComponent();
        ApplyTheme(isDark);
        _onApply = onApply;

        // Deep-copy settings so Cancel doesn't mutate the original
        _settings = new SyntaxColorSettings
        {
            DarkColors = new Dictionary<string, string>(settings.DarkColors),
            LightColors = new Dictionary<string, string>(settings.LightColors)
        };

        _editingDark = isDark;
        darkRadio.IsChecked = isDark;
        lightRadio.IsChecked = !isDark;

        BuildColorRows();
        LoadCurrentColors();
    }

    // ──────────────────────────── theme ────────────────────────────
    private void ApplyTheme(bool dark)
    {
        var themeFile = dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        var themeUri = new Uri(themeFile, UriKind.Relative);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
    }

    // ──────────────────────────── build rows ────────────────────────────
    private void BuildColorRows()
    {
        colorPanel.Children.Clear();
        _hexBoxes.Clear();
        _swatches.Clear();

        AddSection("XML", SyntaxColorSettings.XmlKeys);
        AddSection("JSON", SyntaxColorSettings.JsonKeys);
        AddSection("EDIFACT", SyntaxColorSettings.EdiKeys);
        AddSection("YAML", SyntaxColorSettings.YamlKeys);
    }

    private void AddSection(string title, string[] keys)
    {
        var header = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6)
        };
        header.SetResourceReference(TextBlock.ForegroundProperty, "LabelForeground");
        colorPanel.Children.Add(header);

        var separator = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 6) };
        separator.SetResourceReference(Border.BackgroundProperty, "GridSplitterColor");
        colorPanel.Children.Add(separator);

        foreach (var key in keys)
            AddColorRow(key);
    }

    private void AddColorRow(string key)
    {
        var displayName = SyntaxColorSettings.DisplayNames.TryGetValue(key, out var dn) ? dn : key;

        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Label
        var label = new TextBlock
        {
            Text = displayName,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "EditorForeground");
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        // Color swatch
        var swatch = new Rectangle
        {
            Width = 22,
            Height = 22,
            RadiusX = 3,
            RadiusY = 3,
            Stroke = Brushes.Gray,
            StrokeThickness = 1,
            Cursor = Cursors.Hand,
            ToolTip = "Click to pick a color"
        };
        swatch.Tag = key;
        swatch.MouseLeftButtonDown += Swatch_Click;
        Grid.SetColumn(swatch, 1);
        grid.Children.Add(swatch);
        _swatches[key] = swatch;

        // Hex text box
        var hexBox = new TextBox
        {
            Width = 80,
            Height = 24,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center,
            MaxLength = 7,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(4, 2, 4, 2)
        };
        hexBox.SetResourceReference(TextBox.BackgroundProperty, "EditorBackground");
        hexBox.SetResourceReference(TextBox.ForegroundProperty, "EditorForeground");
        hexBox.SetResourceReference(TextBox.BorderBrushProperty, "GridSplitterColor");
        hexBox.Tag = key;
        hexBox.TextChanged += HexBox_TextChanged;
        Grid.SetColumn(hexBox, 2);
        grid.Children.Add(hexBox);
        _hexBoxes[key] = hexBox;

        colorPanel.Children.Add(grid);
    }

    // ──────────────────────────── load / save ────────────────────────────
    private void LoadCurrentColors()
    {
        foreach (var key in SyntaxColorSettings.AllKeys)
        {
            var hex = _settings.GetColor(key, _editingDark);
            _hexBoxes[key].Text = hex;
            UpdateSwatch(key, hex);
        }
    }

    private void SaveCurrentColors()
    {
        var dict = _editingDark ? _settings.DarkColors : _settings.LightColors;
        foreach (var key in SyntaxColorSettings.AllKeys)
        {
            var hex = _hexBoxes[key].Text?.Trim();
            if (SyntaxColorSettings.IsValidHex(hex))
                dict[key] = hex!;
        }
    }

    private void UpdateSwatch(string key, string? hex)
    {
        if (_swatches.TryGetValue(key, out var swatch))
        {
            try
            {
                if (SyntaxColorSettings.IsValidHex(hex))
                    swatch.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex!));
                else
                    swatch.Fill = Brushes.Transparent;
            }
            catch { swatch.Fill = Brushes.Transparent; }
        }
    }

    // ──────────────────────────── events ────────────────────────────
    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is string key)
            UpdateSwatch(key, tb.Text?.Trim());
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SaveCurrentColors();
        _editingDark = darkRadio.IsChecked == true;
        LoadCurrentColors();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var defaults = _editingDark
            ? SyntaxColorSettings.DefaultDarkColors
            : SyntaxColorSettings.DefaultLightColors;

        var dict = _editingDark ? _settings.DarkColors : _settings.LightColors;
        foreach (var kvp in defaults)
            dict[kvp.Key] = kvp.Value;

        LoadCurrentColors();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentColors();
        Result = _settings;
        DialogResult = true;
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentColors();
        Result = _settings;
        _onApply?.Invoke(_settings);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ──────────────────────────── Win32 color picker ────────────────────────────
    private void Swatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rect || rect.Tag is not string key) return;
        if (!_hexBoxes.TryGetValue(key, out var hexBox)) return;

        var currentHex = hexBox.Text?.Trim();
        Color initial = Colors.White;
        try
        {
            if (SyntaxColorSettings.IsValidHex(currentHex))
                initial = (Color)ColorConverter.ConvertFromString(currentHex!);
        }
        catch { }

        if (ShowColorDialog(initial, out var chosen))
        {
            var hex = $"#{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}";
            hexBox.Text = hex;
        }
    }

    private bool ShowColorDialog(Color initial, out Color result)
    {
        result = initial;
        int initRef = initial.R | (initial.G << 8) | (initial.B << 16);

        var custColors = new int[16];
        var cc = new CHOOSECOLOR
        {
            lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
            hwndOwner = new System.Windows.Interop.WindowInteropHelper(this).Handle,
            rgbResult = initRef,
            Flags = CC_FULLOPEN | CC_RGBINIT
        };

        var handle = GCHandle.Alloc(custColors, GCHandleType.Pinned);
        try
        {
            cc.lpCustColors = handle.AddrOfPinnedObject();
            if (!ChooseColorW(ref cc)) return false;

            result = Color.FromRgb(
                (byte)(cc.rgbResult & 0xFF),
                (byte)((cc.rgbResult >> 8) & 0xFF),
                (byte)((cc.rgbResult >> 16) & 0xFF));
            return true;
        }
        finally { handle.Free(); }
    }

    // ──────────────────────────── P/Invoke ────────────────────────────
    private const int CC_FULLOPEN = 0x00000002;
    private const int CC_RGBINIT = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ChooseColorW(ref CHOOSECOLOR cc);
}
