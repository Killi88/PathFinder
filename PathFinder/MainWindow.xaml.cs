using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PathFinder.Models;
using PathFinder.Services;

namespace PathFinder;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand CloseTabCommand = new RoutedCommand();
    public static readonly RoutedCommand HelpCommand = new RoutedCommand();
    public static readonly RoutedCommand GoToLineCommand = new RoutedCommand();
    public static readonly RoutedCommand MinifyCommand = new RoutedCommand();
    public static readonly RoutedCommand ReplaceCommand = new RoutedCommand();
    public static readonly RoutedCommand SaveAllCommand = new RoutedCommand();

    // ──────────────────────────── line highlight renderer ────────────────────────────
    private sealed class LineHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private Brush? _highlightBrush;

        public int? Line { get; set; }
        public KnownLayer Layer => KnownLayer.Background;

        public LineHighlightRenderer(TextEditor editor) => _editor = editor;

        public void InvalidateBrush() => _highlightBrush = null;

        private Brush HighlightBrush => _highlightBrush ??=
            _editor.TryFindResource("ResultHighlightBrush") as Brush ?? MakeFallbackBrush();

        private static Brush MakeFallbackBrush()
        {
            var b = new SolidColorBrush(Color.FromArgb(80, 0x00, 0x7A, 0xCC));
            b.Freeze();
            return b;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Line is not int line) return;
            if (line < 1 || line > _editor.Document.LineCount) return;

            var docLine = _editor.Document.GetLineByNumber(line);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, docLine))
            {
                drawingContext.DrawRectangle(
                    HighlightBrush, null,
                    new Rect(0, rect.Top, textView.ActualWidth, rect.Height));
            }
        }
    }


    private sealed class EditorTab
    {
        public TabItem TabItem { get; }
        public TextEditor Editor { get; }
        public LineHighlightRenderer HighlightRenderer { get; }
        public SearchPanel SearchPanel { get; set; } = null!;
        public TreeView? InlineTreeView { get; set; }
        public Button? TextViewButton { get; set; }
        public Button? GridViewButton { get; set; }
        public bool IsGridMode { get; set; }
        public string? FilePath { get; set; }
        public FileType FileType { get; set; } = FileType.None;
        public int RightClickLine { get; set; }
        public int RightClickColumn { get; set; }
        public bool IsModified { get; set; }
        public EncodingOption Encoding { get; set; } = EncodingService.SupportedEncodings[0];
        public FileSystemWatcher? Watcher { get; set; }
        public bool HasExternalChange { get; set; }
        public bool IsPinned { get; set; }
        public Button? PinButton { get; set; }
        public TextBlock? ZoomLabel { get; set; }
        public bool HasSyntaxError { get; set; }
        public FoldingManager? FoldingManager { get; set; }
        public TextBlock? SearchMatchLabel { get; set; }
        public ScrollViewer? SchemaTreeView { get; set; }
        public Button? SchemaTreeButton { get; set; }
        public bool IsSchemaTreeMode { get; set; }
        public bool IsSchemaDetected { get; set; }
        public List<Models.SchemaNode>? CachedSchemaNodes { get; set; }
        public FrameworkElement? CachedSchemaPanel { get; set; }
        public Border? SchemaFilterBar { get; set; }
        public TextBox? SchemaFilterBox { get; set; }

        public EditorTab(TabItem ti, TextEditor ed)
        {
            TabItem = ti;
            Editor = ed;
            HighlightRenderer = new LineHighlightRenderer(ed);
            ed.TextArea.TextView.BackgroundRenderers.Add(HighlightRenderer);
        }

        public string TabTitle => FilePath is null ? "untitled"
                                                   : System.IO.Path.GetFileName(FilePath);
    }

    private enum FileType { None, Xml, Json, Edi, Yaml }

    // ──────────────────────────── window settings ────────────────────────────
    internal record WindowLayoutSettings(
        double Left,
        double Top,
        double Width,
        double Height,
        bool IsMaximized,
        double RightPanelWidth,
        List<string>? OpenFiles = null,
        List<string>? PinnedFiles = null,
        bool IsDarkMode = true,
        SyntaxColorSettings? CustomColors = null,
        double AllPathsPathColumnWidth = 250,
        double AllPathsValueColumnWidth = 150,
        List<string>? DcsaSchemaUrls = null,
        List<string>? RecentFiles = null,
        List<ToolbarItemConfig>? ToolbarLayout = null);

    internal static readonly List<string> DefaultDcsaSchemaUrls =
    [
        "https://api.swaggerhub.com/apis/dcsaorg/DCSA_EBL",
        "https://api.swaggerhub.com/apis/dcsaorg/DCSA_BKG"
    ];

    internal static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PathFinder",
        "settings.json");

    internal static WindowLayoutSettings? LoadWindowSettings(string? path = null)
    {
        try
        {
            path ??= SettingsFilePath;
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<WindowLayoutSettings>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    internal static void SaveWindowSettings(WindowLayoutSettings settings, string? path = null)
    {
        try
        {
            path ??= SettingsFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings));
        }
        catch { }
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var vr = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var vb = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        var cx = left + width / 2;
        var cy = top + height / 2;
        return cx >= SystemParameters.VirtualScreenLeft && cx <= vr
            && cy >= SystemParameters.VirtualScreenTop && cy <= vb;
    }

    private (double PathWidth, double ValueWidth) GetAllPathsColumnWidths()
    {
        if (allPathsList.View is GridView gv && gv.Columns.Count >= 2)
        {
            var pw = gv.Columns[0].Width;
            var vw = gv.Columns[1].Width;
            if (double.IsNaN(pw)) pw = gv.Columns[0].ActualWidth;
            if (double.IsNaN(vw)) vw = gv.Columns[1].ActualWidth;
            if (pw > 0 && vw > 0) return (pw, vw);
        }
        return (250, 150);
    }

    private void RestoreAllPathsColumnWidths(WindowLayoutSettings? settings)
    {
        if (settings is null) return;
        if (allPathsList.View is GridView gv && gv.Columns.Count >= 2)
        {
            if (settings.AllPathsPathColumnWidth > 0)
                gv.Columns[0].Width = settings.AllPathsPathColumnWidth;
            if (settings.AllPathsValueColumnWidth > 0)
                gv.Columns[1].Width = settings.AllPathsValueColumnWidth;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    private void AttachFileWatcher(EditorTab tab)
    {
        tab.Watcher?.Dispose();
        tab.Watcher = null;

        if (tab.FilePath is null || !File.Exists(tab.FilePath)) return;

        var dir = Path.GetDirectoryName(tab.FilePath)!;
        var file = Path.GetFileName(tab.FilePath);
        var watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += (s, e) =>
        {
            // Disable immediately to coalesce rapid double-writes from other editors
            watcher.EnableRaisingEvents = false;
            Dispatcher.BeginInvoke(() => OnExternalFileChange(tab));
        };

        tab.Watcher = watcher;
    }

    private void OnExternalFileChange(EditorTab tab)
    {
        if (tab.FilePath is null || !File.Exists(tab.FilePath))
        {
            tab.Watcher?.Dispose();
            tab.Watcher = null;
            return;
        }

        tab.HasExternalChange = true;
        // Re-enable watching so further changes are also tracked
        if (tab.Watcher is not null)
            tab.Watcher.EnableRaisingEvents = true;
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        // Prompt for any tabs that were changed externally while PathFinder was in the background
        var changed = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t?.HasExternalChange == true)
            .ToList();

        foreach (var tab in changed)
        {
            if (tab is null || tab.FilePath is null) continue;
            tab.HasExternalChange = false;

            var name = Path.GetFileName(tab.FilePath);
            var result = MessageBox.Show(
                this,
                $"'{name}' has been modified by another program.\nDo you want to reload it?",
                "File Changed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var (content, encoding) = EncodingService.ReadFileWithEncoding(tab.FilePath);
                    tab.Editor.Text = content;
                    tab.Encoding = encoding;
                    tab.IsModified = false;
                    RefreshTabTitle(tab);
                    if (tab.TabItem.IsSelected) UpdateStatusBar(tab);
                    SetStatus($"Reloaded: {name}");
                }
                catch (Exception ex) { ShowError($"Failed to reload file:\n{ex.Message}"); }
            }
        }
    }

    // ──────────────────────────── instance state ────────────────────────────
    private static IHighlightingDefinition? _xmlHighlighting;
    private static IHighlightingDefinition? _jsonHighlighting;
    private static IHighlightingDefinition? _xmlHighlightingLight;
    private static IHighlightingDefinition? _jsonHighlightingLight;
    private static IHighlightingDefinition? _ediHighlighting;
    private static IHighlightingDefinition? _ediHighlightingLight;
    private static IHighlightingDefinition? _yamlHighlighting;
    private static IHighlightingDefinition? _yamlHighlightingLight;
    private static SyntaxColorSettings _colorSettings = SyntaxColorSettings.Load();
    private bool _isDarkMode = true;
    private List<string> _dcsaSchemaUrls = new(DefaultDcsaSchemaUrls);
    private List<string> _recentFiles = [];
    private readonly List<string> _queryHistory = [];
    private int _queryHistoryIndex = -1;

    private static void ClearHighlightingCache()
    {
        _xmlHighlighting = null;
        _jsonHighlighting = null;
        _xmlHighlightingLight = null;
        _jsonHighlightingLight = null;
        _ediHighlighting = null;
        _ediHighlightingLight = null;
        _yamlHighlighting = null;
        _yamlHighlightingLight = null;
    }
    // Tab drag-drop state
    private Point _tabDragStart;
    private bool _tabDragging;
    private System.Windows.Threading.DispatcherTimer? _allPathsDebounceTimer;
    private System.Windows.Threading.DispatcherTimer? _syntaxValidationTimer;
    private System.Windows.Threading.DispatcherTimer? _schemaFilterDebounceTimer;
    private XPathResultItem? _allPathsContextItem;
    private List<XPathResultItem> _allPathsFullList = [];
    private ReplaceWindow? _replaceWindow;
    // ──────────────────────────── zoom ────────────────────────────
    private const double EditorFontSizeDefault = 13.0;
    private const double EditorFontSizeMin = 6.0;
    private const double EditorFontSizeMax = 48.0;
    private const double RightPanelFontSizeDefault = 11.0;
    private const double RightPanelFontSizeMin = 6.0;
    private const double RightPanelFontSizeMax = 36.0;
    private double _rightPanelFontSize = RightPanelFontSizeDefault;
    private string? _lastOpenDirectory;

    // ──────────────────────────── toolbar ────────────────────────────
    private List<ToolbarItemConfig> _toolbarLayout = new(DefaultToolbarLayout);
    private readonly Dictionary<string, Button> _toolbarButtons = new();
    private readonly Dictionary<string, TextBlock> _toolbarDynamicLabels = new();

    public sealed record ToolbarButtonDefinition(
        string Id,
        string Icon,
        double IconFontSize,
        string Label,
        string Tooltip,
        Action<object, RoutedEventArgs> ClickHandler,
        string? XName = null,
        string? DynamicLabelKey = null,
        bool HasDualIcon = false,
        string? SecondIcon = null,
        double SecondIconFontSize = 10);

    private ToolbarButtonDefinition[] ToolbarButtonRegistry => [
        new("New",          "📄", 14, "New",           "New File (Ctrl+N)",                                       (s,e) => NewFile()),
        new("Open",         "📂", 14, "Open",          "Open File (Ctrl+O)",                                      (s,e) => OpenFile()),
        new("Save",         "💾", 14, "Save",          "Save File (Ctrl+S)",                                      (s,e) => SaveFile()),
        new("SaveAll",      "💾", 14, "Save All",      "Save All (Ctrl+Shift+S)",                                 (s,e) => SaveAll(),       HasDualIcon: true, SecondIcon: "💾", SecondIconFontSize: 10),
        new("Undo",         "↩",  14, "Undo",          "Undo (Ctrl+Z)",                                           (s,e) => { if (ActiveTab?.Editor.CanUndo == true) ActiveTab.Editor.Undo(); }),
        new("Redo",         "↪",  14, "Redo",          "Redo (Ctrl+Y)",                                           (s,e) => { if (ActiveTab?.Editor.CanRedo == true) ActiveTab.Editor.Redo(); }),
        new("AutoIndent",   "⌨",  14, "Auto Indent",   "Auto Indent (Ctrl+Shift+F)",                              (s,e) => FormatDocument()),
        new("Minify",       "⊟",  14, "Minify",        "Minify (Ctrl+Shift+M)",                                   (s,e) => MinifyDocument(), XName: "minifyButton"),
        new("WordWrap",     "↵",  14, "Word Wrap",     "Toggle Word Wrap",                                        (s,e) => ToggleWordWrap(), XName: "wordWrapButton"),
        new("Whitespace",   "·",  14, "Whitespace",    "Show Whitespace Characters",                              (s,e) => ToggleShowWhitespace(), XName: "showWhitespaceButton"),
        new("ConvertFormat","⇄",  14, "To JSON",       "Convert XML ↔ JSON",                                      (s,e) => ConvertFormat(),  XName: "convertFormatButton",  DynamicLabelKey: "convertFormatLabel"),
        new("ConvertYaml",  "⇄",  14, "To YAML",       "Convert to YAML",                                         (s,e) => ConvertToYaml(),  XName: "convertToYamlButton",  DynamicLabelKey: "convertToYamlLabel"),
        new("SampleXml",    "⚙",  14, "Sample XML",    "Generate a sample XML document from this XSD schema",      (s,e) => GenerateSampleXmlFromXsd(), XName: "generateSampleXmlButton"),
        new("SampleJson",   "⚙",  14, "Sample JSON",   "Generate a sample JSON document from this JSON/YAML Schema",(s,e) => GenerateSampleJsonFromSchema(), XName: "generateSampleJsonButton"),
        new("ValidateXsd",  "✓",  14, "Validate",      "Validate XML against an XSD schema",                      (s,e) => OpenValidateXsdDialog(), XName: "validateXsdButton", DynamicLabelKey: "validateXsdLabel"),
        new("CopyExcel",    "📋", 14, "Copy for Excel", "Copy to Excel (copies syntax-highlighted HTML to clipboard)", (s,e) => CopyToExcel(), XName: "copyToExcelButton"),
        new("SortDcsa",     "⇅",  14, "Sort DCSA",     "Sort JSON properties by DCSA schema field order",         (s,e) => SortDcsa_Click(s, (RoutedEventArgs)e), XName: "sortDcsaButton"),
    ];

    internal static readonly List<ToolbarItemConfig> DefaultToolbarLayout =
    [
        new("New"), new("Open"), new("Save"), new("SaveAll"),
        new(ToolbarItemConfig.SeparatorId),
        new("Undo"), new("Redo"),
        new(ToolbarItemConfig.SeparatorId),
        new("AutoIndent"), new("Minify"), new("WordWrap"), new("Whitespace"),
        new(ToolbarItemConfig.SeparatorId),
        new("ConvertFormat"), new("ConvertYaml"), new("SampleXml"), new("SampleJson"), new("ValidateXsd"),
        new(ToolbarItemConfig.SeparatorId),
        new("CopyExcel"),
        new(ToolbarItemConfig.SeparatorId),
        new("SortDcsa"),
    ];

    // ──────────────────────────── init ────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        var saved = LoadWindowSettings();
        if (saved is not null && IsOnScreen(saved.Left, saved.Top, saved.Width, saved.Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
            Height = saved.Height;
            if (saved.IsMaximized) WindowState = WindowState.Maximized;
            rightPanelColumn.Width = new GridLength(saved.RightPanelWidth, GridUnitType.Pixel);
        }
        RestoreAllPathsColumnWidths(saved);

        _allPathsDebounceTimer = new System.Windows.Threading.DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(600) };
        _allPathsDebounceTimer.Tick += (s, e) =>
        {
            _allPathsDebounceTimer.Stop();
            if (allPathsTab.IsSelected)
                PopulateAllPaths(ActiveTab);
        };

        _schemaFilterDebounceTimer = new System.Windows.Threading.DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(300) };
        _schemaFilterDebounceTimer.Tick += (s, e) =>
        {
            _schemaFilterDebounceTimer.Stop();
            var tab = ActiveTab;
            if (tab is not null) ApplySchemaFilter(tab);
        };

        _syntaxValidationTimer = new System.Windows.Threading.DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(600) };
        _syntaxValidationTimer.Tick += (s, e) =>
        {
            _syntaxValidationTimer.Stop();
            var tab = ActiveTab;
            if (tab is not null)
            {
                if (tab.FilePath is null || tab.FileType == FileType.None)
                {
                    var detected = DetectContentFileType(tab.Editor.Text);
                    if (detected != tab.FileType)
                        ApplyFileTypeToTab(tab, detected);
                }
                ValidateSyntax(tab);
                UpdateFolding(tab);
                tab.CachedSchemaNodes = null;
                tab.CachedSchemaPanel = null;
                bool wasDetected = tab.IsSchemaDetected;
                tab.IsSchemaDetected = DetectSchemaContent(tab);
                if (tab.IsSchemaDetected != wasDetected)
                    UpdateSchemaButtonVisibility(tab);
            }
        };
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Check for unsaved changes across all tabs
        var unsavedTabs = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t?.IsModified == true)
            .ToList();

        if (unsavedTabs.Count > 0)
        {
            var names = string.Join("\n", unsavedTabs.Select(t =>
                t!.FilePath is null ? "untitled" : Path.GetFileName(t.FilePath)));
            var result = MessageBox.Show(
                $"The following files have unsaved changes:\n\n{names}\n\nDo you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (result == MessageBoxResult.Yes)
            {
                foreach (var tab in unsavedTabs)
                {
                    editorTabs.SelectedItem = tab!.TabItem;
                    SaveFile();
                    if (tab.IsModified) { e.Cancel = true; return; }
                }
            }
        }

        // Dispose all file watchers before the window closes
        foreach (var ti in editorTabs.Items.OfType<TabItem>())
            if (ti.Tag is EditorTab t) { t.Watcher?.Dispose(); t.Watcher = null; }

        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        var openFiles = editorTabs.Items.OfType<TabItem>()
            .Select(ti => (ti.Tag as EditorTab)?.FilePath)
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        var pinnedFiles = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t?.IsPinned == true && t.FilePath is not null)
            .Select(t => t!.FilePath!)
            .ToList();
        var (pathW, valueW) = GetAllPathsColumnWidths();
        SaveWindowSettings(new WindowLayoutSettings(
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            WindowState == WindowState.Maximized,
            rightPanelColumn.Width.Value,
            openFiles,
            pinnedFiles,
            _isDarkMode,
            AllPathsPathColumnWidth: pathW,
            AllPathsValueColumnWidth: valueW,
            DcsaSchemaUrls: _dcsaSchemaUrls,
            RecentFiles: _recentFiles,
            ToolbarLayout: _toolbarLayout));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply saved theme (defaults to dark)
        var themeSettings = LoadWindowSettings();
        ApplyTheme(themeSettings?.IsDarkMode ?? true);
        if (themeSettings?.DcsaSchemaUrls is { Count: > 0 } urls)
            _dcsaSchemaUrls = new List<string>(urls);
        if (themeSettings?.RecentFiles is { Count: > 0 } recent)
            _recentFiles = new List<string>(recent);
        if (themeSettings?.ToolbarLayout is { Count: > 0 } tbLayout)
            _toolbarLayout = new List<ToolbarItemConfig>(tbLayout);
        BuildToolbar();
        PopulateRecentFilesMenu();

        // Restore previously open files, or start with one blank tab
        var savedSettings = LoadWindowSettings();
        var savedFiles = savedSettings?.OpenFiles
            ?.Where(File.Exists)
            .ToList();
        var pinnedPaths = new HashSet<string>(
            savedSettings?.PinnedFiles ?? [],
            StringComparer.OrdinalIgnoreCase);
        if (savedFiles is { Count: > 0 })
        {
            // Open pinned files first so they appear at the left of the tab strip
            var ordered = savedFiles
                .OrderByDescending(f => pinnedPaths.Contains(f))
                .ToList();
            foreach (var file in ordered)
            {
                var tab = OpenFileFromPath(file);
                if (tab is not null && pinnedPaths.Contains(file))
                    TogglePinTab(tab);
            }
        }
        else
        {
            AddEditorTab(null, string.Empty);
        }

        // File drag-drop: Explorer → Window
        PreviewDrop += Window_FileDrop;
        PreviewDragOver += Window_DragOver;

        // Show file-change prompts only when PathFinder is activated
        Activated += Window_Activated;

        // Refresh All Paths panel when that tab is activated in the right panel
        rightTabs.SelectionChanged += (s, e) =>
        {
            if (allPathsTab.IsSelected)
                PopulateAllPaths(ActiveTab);
        };

        // Build All Paths context menu in code so Click events resolve correctly
        var copyPathMenuItem = new MenuItem { Header = "Copy Path" };
        copyPathMenuItem.Click += (s, e) =>
        {
            if (_allPathsContextItem?.XPath is string p)
            {
                Clipboard.SetDataObject(p, true);
                SetStatus($"Copied: {p}");
            }
        };
        var copyValueMenuItem = new MenuItem { Header = "Copy Value" };
        copyValueMenuItem.Click += (s, e) =>
        {
            if (_allPathsContextItem?.Preview is string v)
            {
                Clipboard.SetDataObject(v, true);
                SetStatus($"Copied: {v}");
            }
        };
        var allPathsCtxMenu = new ContextMenu();
        allPathsCtxMenu.Items.Add(copyPathMenuItem);
        allPathsCtxMenu.Items.Add(copyValueMenuItem);
        allPathsList.ContextMenu = allPathsCtxMenu;

        // Build Messages context menu so the Copy Click handler resolves correctly
        var copyMessagesMenuItem = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        copyMessagesMenuItem.Click += (s, e) => ApplicationCommands.Copy.Execute(null, messagesList);
        var messagesCtxMenu = new ContextMenu();
        messagesCtxMenu.Items.Add(copyMessagesMenuItem);
        messagesList.ContextMenu = messagesCtxMenu;

        // Track which item was right-clicked so the context menu handlers can read it
        allPathsList.PreviewMouseRightButtonDown += (s, e) =>

        {
            var hit = VisualTreeHelper.HitTest(allPathsList, e.GetPosition(allPathsList));
            DependencyObject? obj = hit?.VisualHit;
            while (obj is not null and not ListBoxItem)
                obj = VisualTreeHelper.GetParent(obj);
            _allPathsContextItem = (obj as ListBoxItem)?.DataContext as XPathResultItem;
        };

        // Ctrl+Scroll zoom on right panel
        rightTabs.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            ChangeRightPanelZoom(e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        };

        // Click zoom label to reset
        rightPanelZoomLabel.MouseLeftButtonDown += (s, e) => ResetRightPanelZoom();
    }

    // ──────────────────────────── theme ────────────────────────────
    private void ApplyTheme(bool dark)
    {
        _isDarkMode = dark;

        // Load and apply the appropriate theme dictionary
        var uri = new Uri(
            dark ? "pack://application:,,,/PathFinder;component/Themes/DarkTheme.xaml"
                 : "pack://application:,,,/PathFinder;component/Themes/LightTheme.xaml",
            UriKind.Absolute);

        this.Resources.MergedDictionaries.Clear();
        this.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });

        // Update toggle button icon — show what the button will switch TO
        if (ThemeToggleIcon is TextBlock icon)
        {
            icon.Text = dark ? "☀️" : "🌙";
            ThemeToggleButton.ToolTip = dark ? "Switch to Light Mode" : "Switch to Dark Mode";
        }

        // Update all open editor tabs
        var editorBg = (Brush)FindResource("EditorBackground");
        var editorFg = (Brush)FindResource("EditorForeground");
        var lineNumFg = (Brush)FindResource("LineNumberForeground");

        // Update the Replace window theme if it's open
        if (_replaceWindow is not null && _replaceWindow.IsVisible)
            _replaceWindow.ApplyTheme(dark);

        foreach (var ti in editorTabs.Items.OfType<TabItem>())
        {
            if (ti.Tag is not EditorTab tab) continue;

            tab.Editor.Background = editorBg;
            tab.Editor.Foreground = editorFg;
            tab.Editor.LineNumbersForeground = lineNumFg;
            tab.HighlightRenderer?.InvalidateBrush();
            tab.Editor.TextArea.TextView.Redraw();

            // Switch to the correct highlighting palette for the new theme
            tab.Editor.SyntaxHighlighting = tab.FileType switch
            {
                FileType.Xml => GetOrCreateXmlHighlighting(dark),
                FileType.Json => GetOrCreateJsonHighlighting(dark),
                FileType.Edi => GetOrCreateEdiHighlighting(dark),
                FileType.Yaml => GetOrCreateYamlHighlighting(dark),
                _ => null
            };

            // Rebuild grid if it's currently visible so node colors update
            if (tab.IsGridMode)
                SwitchToGridView(tab);
        }

        // Rebuild toolbar so dynamic-resource colors take effect
        BuildToolbar();
    }

    // ──────────────────────────── toolbar builder ────────────────────────────
    private void BuildToolbar()
    {
        toolbarPanel.Children.Clear();
        _toolbarButtons.Clear();
        _toolbarDynamicLabels.Clear();

        var registry = ToolbarButtonRegistry.ToDictionary(d => d.Id);
        var toolbarStyle = (Style)FindResource("ToolbarButton");

        foreach (var item in _toolbarLayout)
        {
            if (item.IsSeparator)
            {
                var sep = new System.Windows.Shapes.Rectangle { Width = 1, Margin = new Thickness(6, 2, 6, 2) };
                sep.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "SeparatorColor");
                toolbarPanel.Children.Add(sep);
                continue;
            }

            if (!registry.TryGetValue(item.Id, out var def))
                continue; // unknown button id — skip

            var btn = new Button
            {
                Style = toolbarStyle,
                ToolTip = def.Tooltip,
                Margin = new Thickness(2, 0, 0, 0),
            };
            btn.Click += (s, e) => def.ClickHandler(s, e);

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.VerticalAlignment = VerticalAlignment.Center;

            if (def.HasDualIcon && def.SecondIcon is not null)
            {
                var icon1 = new TextBlock
                {
                    Text = def.Icon,
                    FontSize = def.IconFontSize,
                    Margin = new Thickness(0, 0, 1, 0),
                };
                icon1.SetResourceReference(TextBlock.ForegroundProperty, "ButtonForeground");
                sp.Children.Add(icon1);

                var icon2 = new TextBlock
                {
                    Text = def.SecondIcon,
                    FontSize = def.SecondIconFontSize,
                    Margin = new Thickness(0, 0, 5, 0),
                };
                icon2.SetResourceReference(TextBlock.ForegroundProperty, "ButtonForeground");
                sp.Children.Add(icon2);
            }
            else
            {
                var icon = new TextBlock
                {
                    Text = def.Icon,
                    FontSize = def.IconFontSize,
                    Margin = new Thickness(0, 0, 5, 0),
                };
                icon.SetResourceReference(TextBlock.ForegroundProperty, "ButtonForeground");
                sp.Children.Add(icon);
            }

            var label = new TextBlock
            {
                Text = def.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ButtonForeground");
            sp.Children.Add(label);

            btn.Content = sp;
            toolbarPanel.Children.Add(btn);

            if (def.XName is not null)
                _toolbarButtons[def.XName] = btn;

            if (def.DynamicLabelKey is not null)
                _toolbarDynamicLabels[def.DynamicLabelKey] = label;
        }

        // Restore toggle button visual state
        RestoreToolbarToggleStates();
    }

    private void RestoreToolbarToggleStates()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        if (_toolbarButtons.TryGetValue("wordWrapButton", out var wrapBtn))
        {
            wrapBtn.Background = tab.Editor.WordWrap
                ? (Brush)FindResource("AccentBlue")
                : Brushes.Transparent;
        }

        if (_toolbarButtons.TryGetValue("showWhitespaceButton", out var wsBtn))
        {
            wsBtn.Background = tab.Editor.Options.ShowSpaces
                ? (Brush)FindResource("AccentBlue")
                : Brushes.Transparent;
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkMode);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TabItem)))
            return; // let tab reorder drag-over handlers handle it
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_FileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var file in files.Where(
            f => Path.GetExtension(f).ToLowerInvariant() is ".xml" or ".json" or ".xsd" or ".xsl" or ".xslt" or ".edi" or ".yaml" or ".yml"))
        {
            OpenFileFromPath(file);
        }
    }

    // ──────────────────────────── tab management ────────────────────────────
    private EditorTab AddEditorTab(string? filePath, string content, EncodingOption? encoding = null)
    {
        var editor = CreateEditor();
        editor.Text = content;

        var titleBlock = new TextBlock
        {
            Text = filePath is null ? "untitled" : Path.GetFileName(filePath),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TabForeground");

        var closeBtn = new Button
        {
            Content = "✕",
            FontSize = 10,
            Width = 16,
            Height = 16,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Arrow,
            ToolTip = "Close tab"
        };
        closeBtn.SetResourceReference(Control.ForegroundProperty, "TabButtonForeground");

        var pinBtn = new Button
        {
            Content = "📌",
            FontSize = 9,
            Width = 14,
            Height = 14,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Arrow,
            Opacity = 0.25,
            ToolTip = "Pin tab"
        };
        pinBtn.SetResourceReference(Control.ForegroundProperty, "TabButtonForeground");

        var header = new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            LastChildFill = false
        };
        DockPanel.SetDock(pinBtn, Dock.Left);
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(pinBtn);
        header.Children.Add(closeBtn);
        header.Children.Add(titleBlock);

        var tabItem = new TabItem
        {
            Header = header,
            AllowDrop = true
        };

        var state = new EditorTab(tabItem, editor) { FilePath = filePath };
        state.PinButton = pinBtn;
        if (encoding is not null) state.Encoding = encoding;
        state.SearchPanel = SearchPanel.Install(editor.TextArea);
        state.SearchPanel.MarkerBrush = (Brush)FindResource("SearchMarkerBrush");
        state.SearchPanel.Loaded += (s, e) =>
        {
            state.SearchPanel.ApplyTemplate();
            state.SearchMatchLabel = state.SearchPanel.Template.FindName("PART_matchCount", state.SearchPanel) as TextBlock;
        };
        state.SearchPanel.SearchOptionsChanged += (s, e) => RefreshSearchMatchCount(state);
        editor.TextArea.SelectionChanged += (s, e) => RefreshSearchMatchCount(state);
        tabItem.Content = BuildTabViewContainer(state);
        tabItem.Tag = state;

        // ─── Ctrl+Scroll zoom ───
        editor.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            ChangeEditorZoom(state, e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        };

        // Track modifications
        editor.Document.Changed += (s, e) =>
        {
            state.IsModified = true;
            RefreshTabTitle(state);
            _allPathsDebounceTimer?.Stop();
            _allPathsDebounceTimer?.Start();
            _syntaxValidationTimer?.Stop();
            _syntaxValidationTimer?.Start();
        };

        // Caret position → status bar line/column display
        editor.TextArea.Caret.PositionChanged += (s, e) => UpdateLineColumnLabel(state);

        // Close button handler
        closeBtn.Click += (s, e) =>
        {
            e.Handled = true; // don't trigger tab selection
            CloseEditorTab(state);
        };

        // Hover style for close button
        closeBtn.MouseEnter += (s, e) =>
            closeBtn.Foreground = (Brush)FindResource("TabButtonHoverForeground");
        closeBtn.MouseLeave += (s, e) =>
            closeBtn.SetResourceReference(Control.ForegroundProperty, "TabButtonForeground");

        // ─── Pin button ───
        pinBtn.Click += (s, e) =>
        {
            e.Handled = true;
            TogglePinTab(state);
        };
        pinBtn.MouseEnter += (s, e) => { if (!state.IsPinned) pinBtn.Opacity = 0.6; };
        pinBtn.MouseLeave += (s, e) => { if (!state.IsPinned) pinBtn.Opacity = 0.25; };

        // ─── Tab header right-click context menu ───
        var pinMenuItem = new MenuItem { Header = "Pin Tab" };
        var closeMenuItem = new MenuItem { Header = "Close" };
        var closeAllItem = new MenuItem { Header = "Close All" };
        var closeButThisItem = new MenuItem { Header = "Close All But This" };
        var closePinnedItem = new MenuItem { Header = "Close All But Pinned" };

        pinMenuItem.Click += (s, e) => TogglePinTab(state);
        closeMenuItem.Click += (s, e) => CloseEditorTab(state);
        closeAllItem.Click += (s, e) => CloseAllTabs();
        closeButThisItem.Click += (s, e) => CloseAllButThis(state);
        closePinnedItem.Click += (s, e) => CloseAllButPinned();

        var tabCtxMenu = new ContextMenu();
        tabCtxMenu.Items.Add(pinMenuItem);
        tabCtxMenu.Items.Add(new Separator());
        tabCtxMenu.Items.Add(closeMenuItem);
        tabCtxMenu.Items.Add(closeAllItem);
        tabCtxMenu.Items.Add(closeButThisItem);
        tabCtxMenu.Items.Add(closePinnedItem);
        tabCtxMenu.Opened += (s, e) =>
        {
            pinMenuItem.Header = state.IsPinned ? "Unpin Tab" : "Pin Tab";
            bool hasPinned = editorTabs.Items.OfType<TabItem>()
                .Any(ti => (ti.Tag as EditorTab)?.IsPinned == true);
            closePinnedItem.IsEnabled = hasPinned;
        };
        header.ContextMenu = tabCtxMenu;

        // ─── Tab drag-drop reordering ───
        header.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _tabDragStart = e.GetPosition(null);
        };

        header.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _tabDragging) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _tabDragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _tabDragStart.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _tabDragging = true;
                DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.Move);
                _tabDragging = false;
            }
        };

        tabItem.DragOver += (s, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(TabItem))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        };

        tabItem.Drop += (s, e) =>
        {
            if (e.Data.GetData(typeof(TabItem)) is not TabItem sourceTab || sourceTab == tabItem) return;
            int srcIdx = editorTabs.Items.IndexOf(sourceTab);
            int dstIdx = editorTabs.Items.IndexOf(tabItem);
            if (srcIdx < 0 || dstIdx < 0) return;
            editorTabs.Items.Remove(sourceTab);
            editorTabs.Items.Insert(dstIdx, sourceTab);
            editorTabs.SelectedItem = sourceTab;
            e.Handled = true;
        };

        editorTabs.Items.Add(tabItem);
        editorTabs.SelectedItem = tabItem;

        if (filePath is not null)
        {
            var ft = DetectFileType(filePath, content);
            ApplyFileTypeToTab(state, ft);
        }

        // Mark clean after initial load
        state.IsModified = false;

        // ─── XML closing tag autocomplete (Tab to confirm, any other key dismisses) ───
        editor.TextArea.TextEntered += (s, e) =>
        {
            if (e.Text != "/") return;
            if (state.FileType is not FileType.Xml) return;
            var caretOffset = editor.CaretOffset;
            if (caretOffset < 2) return;
            // Only trigger when the character immediately before '/' is '<'
            if (editor.Document.GetCharAt(caretOffset - 2) != '<') return;
            // Scan everything before '</' to find the innermost unclosed tag
            var textBeforeTag = editor.Document.GetText(0, caretOffset - 2);
            var tagName = GetInnermostOpenXmlTag(textBeforeTag);
            if (tagName is null) return;

            // Insert suggestion inline and select it so it appears as highlighted ghost text
            string suggestion = tagName + ">";
            int suggStart = caretOffset;
            int suggEnd = caretOffset + suggestion.Length;
            bool done = false;
            editor.Document.Insert(suggStart, suggestion);
            editor.Select(suggStart, suggestion.Length);

            KeyEventHandler? keyHandler = null;
            TextCompositionEventHandler? textEnteringHandler = null;
            EventHandler? caretHandler = null;

            void Cleanup()
            {
                editor.TextArea.PreviewKeyDown -= keyHandler;
                editor.TextArea.TextEntering -= textEnteringHandler;
                editor.TextArea.Caret.PositionChanged -= caretHandler;
            }

            void Accept()
            {
                if (done) return;
                done = true;
                Cleanup();
                editor.Select(suggEnd, 0); // move caret to end, clear selection
            }

            void Reject()
            {
                if (done) return;
                done = true;
                Cleanup();
                if (editor.Document.TextLength >= suggEnd)
                    editor.Document.Remove(suggStart, suggestion.Length);
                // AvalonEdit adjusts caret offset automatically after document removal
            }

            keyHandler = (_, ke) =>
            {
                switch (ke.Key)
                {
                    case Key.Tab or Key.Return:
                        Accept();
                        ke.Handled = true;
                        break;
                    case Key.Escape:
                        Reject();
                        ke.Handled = true;
                        break;
                    case Key.Left or Key.Right or Key.Up or Key.Down
                         or Key.Home or Key.End or Key.PageUp or Key.PageDown
                         or Key.Back or Key.Delete:
                        Reject();
                        // don't handle — let the key process normally after rejection
                        break;
                }
            };

            textEnteringHandler = (_, _) =>
            {
                // Any typed character while suggestion is active:
                // AvalonEdit will replace the active selection with the typed char,
                // which removes the suggestion text automatically. Just clean up.
                if (done) return;
                done = true;
                Cleanup();
            };

            caretHandler = (_, _) =>
            {
                if (done) return;
                var caret = editor.CaretOffset;
                if (caret < suggStart || caret > suggEnd)
                    Reject(); // user clicked outside the suggestion — dismiss
            };

            editor.TextArea.PreviewKeyDown += keyHandler;
            editor.TextArea.TextEntering += textEnteringHandler;
            editor.TextArea.Caret.PositionChanged += caretHandler;
        };

        AttachFileWatcher(state);
        BuildEditorContextMenu(state);

        // ─── EDIFACT segment hover tooltips ───
        var ediTooltip = new System.Windows.Controls.Primitives.Popup
        {
            StaysOpen = true,
            AllowsTransparency = true,
            IsHitTestVisible = false,
            PlacementTarget = editor.TextArea.TextView,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
        };
        editor.TextArea.TextView.MouseHover += (s, e) =>
        {
            string? content = GetEdifactTooltipContent(state);
            if (content is not null)
            {
                ediTooltip.Child = new Border
                {
                    Background = (Brush)FindResource("EditorBackground"),
                    BorderBrush = (Brush)FindResource("SeparatorColor"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 6, 8, 6),
                    Child = new TextBlock
                    {
                        Text = content,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        Foreground = (Brush)FindResource("EditorForeground")
                    }
                };
                ediTooltip.IsOpen = true;
                e.Handled = true;
            }
        };
        editor.TextArea.TextView.MouseHoverStopped += (s, e) =>
        {
            ediTooltip.IsOpen = false;
        };
        editor.TextArea.TextView.MouseLeave += (s, e) =>
        {
            ediTooltip.IsOpen = false;
        };

        return state;
    }

    private TextEditor CreateEditor()
    {
        var editor = new TextEditor
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, Monospace"),
            FontSize = 13,
            ShowLineNumbers = true,
            WordWrap = false,
            Background = (Brush)FindResource("EditorBackground"),
            Foreground = (Brush)FindResource("EditorForeground"),
            LineNumbersForeground = (Brush)FindResource("LineNumberForeground")
        };
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        return editor;
    }

    /// <summary>
    /// Scans XML text and returns the name of the innermost currently unclosed element,
    /// or null if all elements are properly closed. Used for closing-tag autocomplete.
    /// </summary>
    private static string? GetInnermostOpenXmlTag(string xmlText)
    {
        var stack = new Stack<string>();
        int i = 0;
        int len = xmlText.Length;
        while (i < len)
        {
            if (xmlText[i] != '<') { i++; continue; }
            i++; // consume '<'
            if (i >= len) break;

            // XML comment: <!-- ... -->
            if (i + 2 < len && xmlText[i] == '!' && xmlText[i + 1] == '-' && xmlText[i + 2] == '-')
            {
                i += 3;
                while (i + 2 < len && !(xmlText[i] == '-' && xmlText[i + 1] == '-' && xmlText[i + 2] == '>'))
                    i++;
                i += 3;
                continue;
            }

            // CDATA: <![CDATA[...]]>
            if (i + 7 < len && string.CompareOrdinal(xmlText, i, "![CDATA[", 0, 8) == 0)
            {
                i += 8;
                while (i + 2 < len && !(xmlText[i] == ']' && xmlText[i + 1] == ']' && xmlText[i + 2] == '>'))
                    i++;
                i += 3;
                continue;
            }

            // Other declarations (DOCTYPE, etc.): <!...>
            if (i < len && xmlText[i] == '!')
            {
                while (i < len && xmlText[i] != '>') i++;
                i++;
                continue;
            }

            // Processing instruction: <?...?>
            if (i < len && xmlText[i] == '?')
            {
                while (i + 1 < len && !(xmlText[i] == '?' && xmlText[i + 1] == '>')) i++;
                i += 2;
                continue;
            }

            // Closing tag: </tagname>
            bool isClosing = i < len && xmlText[i] == '/';
            if (isClosing) i++;

            // Read tag name (namespace prefixes like ns:element are valid)
            int nameStart = i;
            while (i < len && xmlText[i] != '>' && xmlText[i] != '/' && !char.IsWhiteSpace(xmlText[i]))
                i++;
            string tagName = xmlText[nameStart..i];
            if (string.IsNullOrEmpty(tagName)) { i++; continue; }

            if (isClosing)
            {
                while (i < len && xmlText[i] != '>') i++;
                i++;
                if (stack.Count > 0 && stack.Peek() == tagName)
                    stack.Pop();
                continue;
            }

            // Skip attributes, handling quoted values so '>' inside attributes is not treated as tag end
            bool inQuote = false;
            char quoteChar = '"';
            bool selfClosing = false;
            while (i < len)
            {
                char ch = xmlText[i];
                if (inQuote)
                {
                    if (ch == quoteChar) inQuote = false;
                }
                else
                {
                    if (ch == '"' || ch == '\'') { inQuote = true; quoteChar = ch; }
                    else if (ch == '/' && i + 1 < len && xmlText[i + 1] == '>') { selfClosing = true; i += 2; break; }
                    else if (ch == '>') { i++; break; }
                }
                i++;
            }

            if (!selfClosing)
                stack.Push(tagName);
        }
        return stack.Count > 0 ? stack.Peek() : null;
    }

    private EditorTab? ActiveTab =>
        editorTabs.SelectedItem is TabItem ti ? ti.Tag as EditorTab : null;

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null) return;

        SetStatus("Ready");
        UpdateStatusBar(tab);
        UpdateRightPanelForTab(tab);
        UpdateLineColumnLabel(tab);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null) return;
        CloseEditorTab(tab);
    }

    private void CloseEditorTab(EditorTab tab)
    {
        if (editorTabs.Items.Count <= 1)
        {
            // Keep at least one tab — just clear it instead of closing
            tab.Watcher?.Dispose();
            tab.Watcher = null;
            tab.FilePath = null;
            tab.IsModified = false;
            tab.HasSyntaxError = false;
            tab.IsPinned = false;
            if (tab.PinButton is not null)
            {
                tab.PinButton.Opacity = 0.25;
                tab.PinButton.SetResourceReference(Control.ForegroundProperty, "TabButtonForeground");
                tab.PinButton.ToolTip = "Pin tab";
            }
            tab.Encoding = EncodingService.SupportedEncodings[0]; // reset to UTF-8
            tab.Editor.Text = string.Empty;
            if (tab.FoldingManager is not null) { FoldingManager.Uninstall(tab.FoldingManager); tab.FoldingManager = null; }
            ApplyFileTypeToTab(tab, FileType.None);
            RefreshTabTitle(tab);
            return;
        }

        if (tab.IsModified)
        {
            var name = tab.FilePath is null ? "untitled" : Path.GetFileName(tab.FilePath);
            var result = MessageBox.Show(
                $"'{name}' has unsaved changes.\nDo you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes)
            {
                SaveFile();
                // User may have cancelled the Save As dialog for untitled files
                if (tab.IsModified) return;
            }
        }

        tab.Watcher?.Dispose();
        tab.Watcher = null;
        if (tab.FoldingManager is not null) { FoldingManager.Uninstall(tab.FoldingManager); tab.FoldingManager = null; }
        editorTabs.Items.Remove(tab.TabItem);
    }

    private void TogglePinTab(EditorTab tab)
    {
        tab.IsPinned = !tab.IsPinned;
        if (tab.PinButton is not null)
        {
            if (tab.IsPinned)
            {
                tab.PinButton.Opacity = 1.0;
                tab.PinButton.Foreground = (Brush)FindResource("PinActiveColor");
                tab.PinButton.ToolTip = "Unpin tab";

                // Move pinned tab to the front (after any existing pinned tabs)
                int pinnedCount = editorTabs.Items.OfType<TabItem>()
                    .Count(ti => ti != tab.TabItem && (ti.Tag as EditorTab)?.IsPinned == true);
                int currentIdx = editorTabs.Items.IndexOf(tab.TabItem);
                if (currentIdx > pinnedCount)
                {
                    editorTabs.Items.Remove(tab.TabItem);
                    editorTabs.Items.Insert(pinnedCount, tab.TabItem);
                    editorTabs.SelectedItem = tab.TabItem;
                }
            }
            else
            {
                tab.PinButton.Opacity = 0.25;
                tab.PinButton.SetResourceReference(Control.ForegroundProperty, "TabButtonForeground");
                tab.PinButton.ToolTip = "Pin tab";

                // Move unpinned tab after the last pinned tab
                int pinnedCount = editorTabs.Items.OfType<TabItem>()
                    .Count(ti => (ti.Tag as EditorTab)?.IsPinned == true);
                int currentIdx = editorTabs.Items.IndexOf(tab.TabItem);
                if (currentIdx < pinnedCount)
                {
                    editorTabs.Items.Remove(tab.TabItem);
                    editorTabs.Items.Insert(pinnedCount, tab.TabItem);
                    editorTabs.SelectedItem = tab.TabItem;
                }
            }
        }
    }

    private void CloseAllTabs()
    {
        var tabs = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t is not null)
            .ToList();
        foreach (var tab in tabs)
            CloseEditorTab(tab!);
    }

    private void CloseAllButThis(EditorTab thisTab)
    {
        var tabs = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t is not null && t != thisTab && !t.IsPinned)
            .ToList();
        foreach (var tab in tabs)
            CloseEditorTab(tab!);
    }

    private void CloseAllButPinned()
    {
        var tabs = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t is not null && !t.IsPinned)
            .ToList();
        foreach (var tab in tabs)
            CloseEditorTab(tab!);
    }

    private void RefreshTabTitle(EditorTab tab)
    {
        if (tab.TabItem.Header is not DockPanel dp) return;
        var tb = dp.Children.OfType<TextBlock>().FirstOrDefault();
        if (tb is not null)
        {
            tb.Inlines.Clear();
            tb.Inlines.Add(new System.Windows.Documents.Run(tab.TabTitle));
            if (tab.HasSyntaxError)
                tb.Inlines.Add(new System.Windows.Documents.Run(" ⚠") { Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x48, 0x47)) });
            if (tab.IsModified)
                tb.Inlines.Add(new System.Windows.Documents.Run(" ●") { Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xB0, 0x4F)) });
        }
    }

    private void ValidateSyntax(EditorTab tab)
    {
        var text = tab.Editor.Text;
        bool hasError = false;
        List<MessageItem>? messages = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            switch (tab.FileType)
            {
                case FileType.Xml:
                    try { XmlService.FormatXml(text); }
                    catch (System.Xml.XmlException ex)
                    {
                        hasError = true;
                        messages = new List<MessageItem>
                        {
                            new() { Message = ex.Message, LineNumber = ex.LineNumber > 0 ? ex.LineNumber : null }
                        };
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        messages = new List<MessageItem> { new() { Message = ex.Message } };
                    }
                    break;
                case FileType.Json:
                    try { JsonService.FormatJson(text); }
                    catch (Newtonsoft.Json.JsonReaderException ex)
                    {
                        hasError = true;
                        messages = new List<MessageItem>
                        {
                            new() { Message = ex.Message, LineNumber = ex.LineNumber > 0 ? ex.LineNumber : null }
                        };
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        messages = new List<MessageItem> { new() { Message = ex.Message } };
                    }
                    break;
                case FileType.Yaml:
                    try { YamlService.FormatYaml(text); }
                    catch (YamlDotNet.Core.YamlException ex)
                    {
                        hasError = true;
                        int? line = ex.Start.Line > 0 ? (int)ex.Start.Line : null;
                        messages = new List<MessageItem>
                        {
                            new() { Message = ex.Message, LineNumber = line }
                        };
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        messages = new List<MessageItem> { new() { Message = ex.Message } };
                    }
                    break;
                case FileType.Edi:
                    var ediError = EdifactService.ValidateEdi(text);
                    var ediDefErrors = EdifactService.ValidateEdiDefinition(text);
                    var allEdiErrors = new List<string>();
                    if (ediError is not null)
                        allEdiErrors.AddRange(ediError.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                    allEdiErrors.AddRange(ediDefErrors);
                    if (allEdiErrors.Count > 0)
                    {
                        hasError = true;
                        messages = ParseValidationErrors(string.Join("\n", allEdiErrors));
                    }
                    break;
            }
        }
        if (tab.HasSyntaxError != hasError)
        {
            tab.HasSyntaxError = hasError;
            RefreshTabTitle(tab);
        }
        if (tab == ActiveTab)
        {
            if (messages is { Count: > 0 })
                ShowMessages(messages);
            else
                ClearMessages();
        }
    }

    private void UpdateTabHeader(EditorTab tab)
    {
        tab.IsModified = false;
        RefreshTabTitle(tab);
    }

    private void UpdateStatusBar(EditorTab tab)
    {
        filePathLabel.Text = tab.FilePath ?? string.Empty;
        fileTypeLabel.Text = tab.FileType switch
        {
            FileType.Xml => tab.FilePath is null ? "XML"
                : Path.GetExtension(tab.FilePath).ToUpperInvariant().TrimStart('.'),
            FileType.Json => "JSON",
            FileType.Edi => "EDI",
            FileType.Yaml => "YAML",
            _ => string.Empty
        };
        UpdateEncodingButton(tab);

        // Enable/disable toolbar buttons (gracefully skip missing ones)
        if (_toolbarButtons.TryGetValue("copyToExcelButton", out var copyExcel))
            copyExcel.IsEnabled = tab.FileType is FileType.Xml or FileType.Json or FileType.Yaml;
        if (_toolbarButtons.TryGetValue("minifyButton", out var minify))
            minify.IsEnabled = tab.FileType is FileType.Xml or FileType.Json or FileType.Edi;
        if (_toolbarButtons.TryGetValue("sortDcsaButton", out var sortDcsa))
            sortDcsa.IsEnabled = tab.FileType == FileType.Json;

        bool canConvert = tab.FileType is FileType.Xml or FileType.Json or FileType.Yaml;
        if (_toolbarButtons.TryGetValue("convertFormatButton", out var convertFmt))
            convertFmt.IsEnabled = canConvert;
        if (_toolbarDynamicLabels.TryGetValue("convertFormatLabel", out var convertFmtLabel))
            convertFmtLabel.Text = tab.FileType == FileType.Json ? "To XML"
                : tab.FileType == FileType.Yaml ? "To JSON" : "To JSON";

        if (_toolbarButtons.TryGetValue("convertToYamlButton", out var convertYaml))
            convertYaml.IsEnabled = canConvert;
        if (_toolbarDynamicLabels.TryGetValue("convertToYamlLabel", out var convertYamlLabel))
            convertYamlLabel.Text = tab.FileType == FileType.Json ? "To YAML"
                : tab.FileType == FileType.Yaml ? "To XML" : "To YAML";

        if (_toolbarButtons.TryGetValue("generateSampleXmlButton", out var sampleXml))
            sampleXml.IsEnabled = tab.FileType == FileType.Xml
                && string.Equals(Path.GetExtension(tab.FilePath), ".xsd", StringComparison.OrdinalIgnoreCase);
        if (_toolbarButtons.TryGetValue("generateSampleJsonButton", out var sampleJson))
            sampleJson.IsEnabled = tab.IsSchemaDetected
                && tab.FileType is FileType.Json or FileType.Yaml;
        if (_toolbarButtons.TryGetValue("validateXsdButton", out var validateXsd))
            validateXsd.IsEnabled = tab.FileType == FileType.Xml;
        if (_toolbarDynamicLabels.TryGetValue("validateXsdLabel", out var validateLabel))
            validateLabel.Text = IsXsdTab(tab) ? "Validate XML" : "Validate vs XSD";
    }

    // ──────────────────────────── encoding picker ────────────────────────────

    private void UpdateEncodingButton(EditorTab tab)
    {
        encodingButton.Content = tab.Encoding.DisplayName;
    }

    private void EncodingButton_Click(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab is null) return;

        var menu = new ContextMenu();
        foreach (var option in EncodingService.SupportedEncodings)
        {
            var item = new MenuItem
            {
                Header = option.DisplayName,
                Background = (System.Windows.Media.Brush)FindResource("ContextMenuBackground"),
                Foreground = (System.Windows.Media.Brush)FindResource("ContextMenuForeground"),
                Template = (ControlTemplate)FindResource("SubmenuItemTemplate"),
            };
            if (option.DisplayName == tab.Encoding.DisplayName)
                item.Icon = new System.Windows.Controls.TextBlock { Text = "✓" };
            var captured = option;
            item.Click += (_, _) => ChangeEncoding(tab, captured);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = encodingButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void ChangeEncoding(EditorTab tab, EncodingOption newEncoding)
    {
        if (tab.FilePath is null)
        {
            // Unsaved file — store the encoding for when it is saved
            tab.Encoding = newEncoding;
            UpdateEncodingButton(tab);
            SetStatus($"Encoding set to: {newEncoding.DisplayName}");
            return;
        }

        try
        {
            if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = false;
            EncodingService.WriteFileWithEncoding(tab.FilePath, tab.Editor.Text, newEncoding);

            // If the XML declaration was updated, sync the editor content
            if (tab.FileType == FileType.Xml)
            {
                var updated = EncodingService.UpdateXmlDeclarationEncoding(
                    tab.Editor.Text, newEncoding.XmlEncodingName);
                if (updated != tab.Editor.Text)
                {
                    tab.Editor.Text = updated;
                    tab.IsModified = false;
                    RefreshTabTitle(tab);
                }
            }

            tab.Encoding = newEncoding;
            UpdateEncodingButton(tab);
            SetStatus($"Saved with encoding: {newEncoding.DisplayName}");
        }
        catch (Exception ex) { ShowError($"Failed to save with encoding:\n{ex.Message}"); }
        finally
        {
            if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateRightPanelForTab(EditorTab tab)
    {
        // XSD / XSL / XSLT are loaded as FileType.Xml for syntax highlighting,
        // but XPath querying only makes sense for plain .xml files.
        var ext = tab.FilePath is null ? string.Empty
                  : Path.GetExtension(tab.FilePath).ToLowerInvariant();
        bool isXml = tab.FileType == FileType.Xml
                      && ext is not (".xsd" or ".xsl" or ".xslt");
        bool isJson = tab.FileType == FileType.Json;
        bool isYaml = tab.FileType == FileType.Yaml;
        bool active = isXml || isJson || isYaml;

        // Block the content area entirely when no XML/JSON/YAML file is open
        xpathContent.IsEnabled = active;
        xpathContent.Opacity = active ? 1.0 : 0.3;

        // Rename the XPath tab according to what's open
        xpathTab.Header = isYaml ? "YAMLPath" : isJson ? "JSONPath" : "XPath";

        // Update the label inside the XPath tab
        xpathLabel.Text = isYaml
            ? "YAMLPath Expression  (Ctrl+Enter to execute):"
            : isJson
            ? "JSONPath Expression  (Ctrl+Enter to execute):"
            : "XPath Expression  (Ctrl+Enter to execute):";
        executeXpathBtn.Content = isYaml
            ? "▶  Execute YAMLPath"
            : isJson
            ? "▶  Execute JSONPath"
            : "▶  Execute XPath";

        // Enable/disable the inline Grid button
        if (tab.GridViewButton is not null)
        {
            bool inlineGridOk = tab.FileType is FileType.Xml or FileType.Json or FileType.Yaml;
            tab.GridViewButton.IsEnabled = inlineGridOk;
            tab.GridViewButton.Opacity = inlineGridOk ? 1.0 : 0.4;
            if (!inlineGridOk && tab.IsGridMode)
                SwitchToTextView(tab);
        }

        // All Paths tab
        allPathsTab.IsEnabled = active;
        allPathsTab.Opacity = active ? 1.0 : 0.6;
        if (allPathsTab.IsSelected)
            PopulateAllPaths(active ? tab : null);
    }

    // ──────────────────────────── keyboard shortcuts ────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.N && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { NewFile(); e.Handled = true; }
        else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { OpenFile(); e.Handled = true; }
        else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { SaveFile(); e.Handled = true; }
        else if (e.Key == Key.F && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { FormatDocument(); e.Handled = true; }
        else if (e.Key == Key.M && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { MinifyDocument(); e.Handled = true; }
        else if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { OpenFind(); e.Handled = true; }
        else if (e.Key == Key.H && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { OpenFindReplace(); e.Handled = true; }
    }

    // ──────────────────────────── File menu ────────────────────────────
    private void Find_Click(object sender, RoutedEventArgs e) => OpenFind();
    private void NewFile_Click(object sender, RoutedEventArgs e) => NewFile();
    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
    private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveFile();
    private void SaveFileAs_Click(object sender, RoutedEventArgs e) => SaveFileAs();
    private void SaveAll_Click(object sender, RoutedEventArgs e) => SaveAll();
    private void Undo_Click(object sender, RoutedEventArgs e) { if (ActiveTab?.Editor.CanUndo == true) ActiveTab.Editor.Undo(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (ActiveTab?.Editor.CanRedo == true) ActiveTab.Editor.Redo(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void FormatDocument_Click(object sender, RoutedEventArgs e) => FormatDocument();
    private void GoToLine_Click(object sender, RoutedEventArgs e) => GoToLine();
    private void ToggleWordWrap_Click(object sender, RoutedEventArgs e) => ToggleWordWrap();
    private void ToggleShowWhitespace_Click(object sender, RoutedEventArgs e) => ToggleShowWhitespace();
    private void CopyToExcel_Click(object sender, RoutedEventArgs e) => CopyToExcel();
    private void SortDcsa_Click(object sender, RoutedEventArgs e) => _ = SortDcsaAsync();
    private void ConvertFormat_Click(object sender, RoutedEventArgs e) => ConvertFormat();

    private void GenerateSampleXml_Click(object sender, RoutedEventArgs e) => GenerateSampleXmlFromXsd();
    private void ValidateXsd_Click(object sender, RoutedEventArgs e) => OpenValidateXsdDialog();
    private void Help_Click(object sender, RoutedEventArgs e) => OpenHelp();
    private void RegisterContextMenu_Click(object sender, RoutedEventArgs e) => RegisterOpenWithContextMenu();
    private void ExportSettings_Click(object sender, RoutedEventArgs e) => ExportSettings();
    private void ImportSettings_Click(object sender, RoutedEventArgs e) => ImportSettings();
    private void CustomizeColors_Click(object sender, RoutedEventArgs e) => CustomizeColors();
    private void DcsaSchemaUrls_Click(object sender, RoutedEventArgs e) => OpenDcsaSchemaUrlSettings();
    private void CustomizeToolbar_Click(object sender, RoutedEventArgs e) => CustomizeToolbar();

    private void CustomizeColors()
    {
        void ApplyColors(SyntaxColorSettings newSettings)
        {
            _colorSettings = newSettings;
            SyntaxColorSettings.Save(_colorSettings);
            ClearHighlightingCache();
            ApplyTheme(_isDarkMode);
            SetStatus("Syntax colors updated");
        }

        var dlg = new SyntaxColorsWindow(_isDarkMode, _colorSettings, ApplyColors) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } newSettings)
            ApplyColors(newSettings);
    }

    private void ExportSettings()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Settings",
            Filter = "PathFinder Settings|*.pathfinder.json|JSON Files|*.json|All Files|*.*",
            FileName = "pathfinder-settings.pathfinder.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var bounds = WindowState == WindowState.Maximized
                ? RestoreBounds
                : new Rect(Left, Top, Width, Height);

            var openFiles = editorTabs.Items.OfType<TabItem>()
                .Select(ti => (ti.Tag as EditorTab)?.FilePath)
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();

            var pinnedFiles = editorTabs.Items.OfType<TabItem>()
                .Select(ti => ti.Tag as EditorTab)
                .Where(t => t?.IsPinned == true && t.FilePath is not null)
                .Select(t => t!.FilePath!)
                .ToList();

            var (pathW, valueW) = GetAllPathsColumnWidths();
            var settings = new WindowLayoutSettings(
                bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                WindowState == WindowState.Maximized,
                rightPanelColumn.Width.Value,
                openFiles,
                pinnedFiles,
                _isDarkMode,
                _colorSettings,
                pathW,
                valueW,
                _dcsaSchemaUrls,
                ToolbarLayout: _toolbarLayout);

            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
            SetStatus($"Settings exported to: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { ShowError($"Failed to export settings:\n{ex.Message}"); }
    }

    private void ImportSettings()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Settings",
            Filter = "PathFinder Settings|*.pathfinder.json|JSON Files|*.json|All Files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        WindowLayoutSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<WindowLayoutSettings>(File.ReadAllText(dlg.FileName));
        }
        catch (Exception ex) { ShowError($"Failed to read settings file:\n{ex.Message}"); return; }

        if (settings is null) { ShowError("The selected file does not contain valid PathFinder settings."); return; }

        var result = MessageBox.Show(
            "Importing settings will:\n" +
            "  • Resize and reposition the window\n" +
            "  • Close all current tabs\n" +
            "  • Reopen the files saved in the settings file\n\n" +
            "Any unsaved changes will be lost. Continue?",
            "Import Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        // Apply window layout
        if (IsOnScreen(settings.Left, settings.Top, settings.Width, settings.Height))
        {
            WindowState = WindowState.Normal;
            Left = settings.Left;
            Top = settings.Top;
            Width = settings.Width;
            Height = settings.Height;
        }
        if (settings.IsMaximized) WindowState = WindowState.Maximized;
        rightPanelColumn.Width = new GridLength(settings.RightPanelWidth, GridUnitType.Pixel);
        RestoreAllPathsColumnWidths(settings);

        // Apply imported theme
        ApplyTheme(settings.IsDarkMode);

        // Save to the live settings file so the layout persists
        SaveWindowSettings(settings);

        // Close all current tabs without save prompts
        var tabs = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t is not null)
            .ToList();
        foreach (var tab in tabs)
        {
            tab!.IsModified = false; // suppress save prompt
            CloseEditorTab(tab);
        }

        // Reopen files, pinned first
        var savedFiles = settings.OpenFiles?.Where(File.Exists).ToList() ?? [];
        var pinnedPaths = new HashSet<string>(
            settings.PinnedFiles ?? [],
            StringComparer.OrdinalIgnoreCase);

        var ordered = savedFiles
            .OrderByDescending(f => pinnedPaths.Contains(f))
            .ToList();

        foreach (var file in ordered)
        {
            var tab = OpenFileFromPath(file);
            if (tab is not null && pinnedPaths.Contains(file))
                TogglePinTab(tab);
        }

        if (editorTabs.Items.Count == 0)
            AddEditorTab(null, string.Empty);

        // Import color settings if present
        if (settings.CustomColors is { } importedColors)
        {
            _colorSettings = importedColors;
            SyntaxColorSettings.Save(_colorSettings);
            ClearHighlightingCache();
            ApplyTheme(_isDarkMode);
        }

        // Import DCSA schema URLs if present
        if (settings.DcsaSchemaUrls is { Count: > 0 } importedUrls)
            _dcsaSchemaUrls = new List<string>(importedUrls);

        // Import toolbar layout if present
        if (settings.ToolbarLayout is { Count: > 0 } importedToolbar)
        {
            _toolbarLayout = new List<ToolbarItemConfig>(importedToolbar);
            BuildToolbar();
        }

        SetStatus($"Settings imported from: {Path.GetFileName(dlg.FileName)}");
    }

    private void OpenHelp()
    {
        var w = new HelpWindow(_isDarkMode) { Owner = this };
        w.ShowDialog();
    }

    private void RegisterOpenWithContextMenu()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                ShowError("Could not determine the PathFinder executable path.");
                return;
            }

            const string keyPath = @"Software\Classes\*\shell\PathFinder";
            using var shellKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);
            shellKey.SetValue("", "Open with PathFinder");
            shellKey.SetValue("Icon", $"\"{exePath}\"");

            using var commandKey = shellKey.CreateSubKey("command");
            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");

            SetStatus("'Open with PathFinder' context menu registered");
            MessageBox.Show(
                "The 'Open with PathFinder' entry has been added to the Windows right-click context menu.\n\n" +
                "Right-click any file in Explorer to see it.",
                "Context Menu Registered",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to register context menu entry:\n{ex.Message}");
        }
    }

    private void OpenFind()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        tab.Editor.Focus();
        tab.SearchPanel.Open();
        RefreshSearchMatchCount(tab);
    }

    private void FindReplace_Click(object sender, RoutedEventArgs e) => OpenFindReplace();

    private void OpenFindReplace()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        if (_replaceWindow is not null && _replaceWindow.IsVisible)
        {
            _replaceWindow.Activate();
            return;
        }
        _replaceWindow = new ReplaceWindow(_isDarkMode, tab.Editor, this, () =>
            editorTabs.Items.OfType<TabItem>()
                .Select(ti => ti.Tag as EditorTab)
                .Where(t => t is not null)
                .Select(t => t!.Editor)
                .ToList());
        _replaceWindow.Show();
    }

    private void RefreshSearchMatchCount(EditorTab tab)
    {
        if (tab.SearchMatchLabel is null) return;
        if (tab.SearchPanel.Visibility != Visibility.Visible)
        {
            tab.SearchMatchLabel.Text = string.Empty;
            return;
        }

        var pattern = tab.SearchPanel.SearchPattern;
        if (string.IsNullOrEmpty(pattern))
        {
            tab.SearchMatchLabel.Text = string.Empty;
            return;
        }

        try
        {
            var mode = tab.SearchPanel.UseRegex ? SearchMode.RegEx : SearchMode.Normal;
            var strategy = SearchStrategyFactory.Create(pattern, !tab.SearchPanel.MatchCase, tab.SearchPanel.WholeWords, mode);
            var doc = tab.Editor.Document;
            var allResults = strategy.FindAll(doc, 0, doc.TextLength)
                .OrderBy(r => r.Offset)
                .ToList();

            int total = allResults.Count;
            if (total == 0)
            {
                tab.SearchMatchLabel.Text = "No results";
                return;
            }

            // Determine current match: prefer exact selection match, then closest before caret
            int selStart = tab.Editor.SelectionStart;
            int selLen = tab.Editor.SelectionLength;
            int caretOffset = tab.Editor.TextArea.Caret.Offset;

            int current = -1;
            if (selLen > 0)
                current = allResults.FindIndex(r => r.Offset == selStart && r.Length == selLen);
            if (current < 0)
            {
                current = allResults.FindLastIndex(r => r.Offset <= caretOffset);
                if (current < 0) current = 0;
            }

            tab.SearchMatchLabel.Text = $"{current + 1} of {total}";
        }
        catch
        {
            tab.SearchMatchLabel.Text = string.Empty;
        }
    }

    private void NewFile() => AddEditorTab(null, string.Empty);

    private void ToggleWordWrap()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        bool wrap = !tab.Editor.WordWrap;
        // Apply to all open tabs
        foreach (var ti in editorTabs.Items.OfType<TabItem>())
            if (ti.Tag is EditorTab t) t.Editor.WordWrap = wrap;
        wordWrapMenuItem.IsChecked = wrap;
        if (_toolbarButtons.TryGetValue("wordWrapButton", out var wrapBtn))
            wrapBtn.Background = wrap
                ? (Brush)FindResource("AccentBlue")
                : Brushes.Transparent;
    }

    private void ToggleShowWhitespace()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        bool show = !tab.Editor.Options.ShowSpaces;
        // Apply to all open tabs
        foreach (var ti in editorTabs.Items.OfType<TabItem>())
        {
            if (ti.Tag is EditorTab t)
            {
                t.Editor.Options.ShowSpaces = show;
                t.Editor.Options.ShowTabs = show;
                t.Editor.Options.ShowEndOfLine = show;
            }
        }
        if (_toolbarButtons.TryGetValue("showWhitespaceButton", out var wsBtn))
            wsBtn.Background = show
                ? (Brush)FindResource("AccentBlue")
                : Brushes.Transparent;
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open File",
            Filter = "XML / JSON / EDI / YAML Files|*.xml;*.json;*.xsd;*.xsl;*.xslt;*.edi;*.yaml;*.yml|XML Files|*.xml|JSON Files|*.json|YAML Files|*.yaml;*.yml|XSD Schema Files|*.xsd|XSL / XSLT Files|*.xsl;*.xslt|EDIFACT Files|*.edi|All Files|*.*",
            Multiselect = true,
            InitialDirectory = _lastOpenDirectory ?? string.Empty
        };
        if (dlg.ShowDialog() != true) return;
        _lastOpenDirectory = Path.GetDirectoryName(dlg.FileNames[0]);
        foreach (var file in dlg.FileNames)
            OpenFileFromPath(file);
    }

    /// <summary>
    /// Opens a file from a command-line argument (e.g. Explorer context menu).
    /// </summary>
    internal void OpenFileFromCommandLine(string filePath) => OpenFileFromPath(filePath);

    private EditorTab? OpenFileFromPath(string filePath)
    {
        // If the file is already open, just switch to that tab
        var existing = editorTabs.Items.OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .FirstOrDefault(t => t?.FilePath is not null &&
                string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            editorTabs.SelectedItem = existing.TabItem;
            AddToRecentFiles(filePath);
            return existing;
        }

        try
        {
            var (content, encoding) = EncodingService.ReadFileWithEncoding(filePath);
            _lastOpenDirectory = Path.GetDirectoryName(filePath);
            var tab = ActiveTab;
            bool reuse = tab is not null && tab.FilePath is null
                          && string.IsNullOrWhiteSpace(tab.Editor.Text);
            if (reuse && tab is not null)
            {
                tab.FilePath = filePath;
                tab.Editor.Text = content;
                tab.IsModified = false;
                tab.Encoding = encoding;
                ApplyFileTypeToTab(tab, DetectFileType(filePath, content));
                UpdateTabHeader(tab);
                ValidateSyntax(tab);
                UpdateStatusBar(tab);
                AttachFileWatcher(tab);
                ClearXPathResults();
                SetStatus($"Opened: {Path.GetFileName(filePath)}");
                AddToRecentFiles(filePath);
                return tab;
            }
            else
            {
                var newTab = AddEditorTab(filePath, content, encoding);
                ValidateSyntax(newTab);
                ClearXPathResults();
                SetStatus($"Opened: {Path.GetFileName(filePath)}");
                AddToRecentFiles(filePath);
                return newTab;
            }
        }
        catch (Exception ex) { ShowError($"Failed to open file:\n{ex.Message}"); return null; }
    }

    private void SaveFile()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        if (tab.FilePath is null) { SaveFileAs(); return; }

        try
        {
            if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = false;
            EncodingService.WriteFileWithEncoding(tab.FilePath, tab.Editor.Text, tab.Encoding);
            // If the XML declaration was updated, sync the editor text
            var updated = EncodingService.UpdateXmlDeclarationEncoding(tab.Editor.Text, tab.Encoding.XmlEncodingName);
            if (updated != tab.Editor.Text)
                tab.Editor.Text = updated;
            tab.IsModified = false;
            RefreshTabTitle(tab);
            SetStatus($"Saved: {Path.GetFileName(tab.FilePath)}");
        }
        catch (Exception ex) { ShowError($"Failed to save file:\n{ex.Message}"); }
        finally
        {
            if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = true;
        }
    }

    private void SaveAll()
    {
        int saved = 0;
        foreach (var ti in editorTabs.Items.OfType<TabItem>())
        {
            if (ti.Tag is not EditorTab tab) continue;
            if (!tab.IsModified) continue;
            if (tab.FilePath is null) continue; // skip untitled tabs

            try
            {
                if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = false;
                EncodingService.WriteFileWithEncoding(tab.FilePath, tab.Editor.Text, tab.Encoding);
                var updated = EncodingService.UpdateXmlDeclarationEncoding(tab.Editor.Text, tab.Encoding.XmlEncodingName);
                if (updated != tab.Editor.Text)
                    tab.Editor.Text = updated;
                tab.IsModified = false;
                RefreshTabTitle(tab);
                saved++;
            }
            catch (Exception ex) { ShowError($"Failed to save {Path.GetFileName(tab.FilePath)}:\n{ex.Message}"); }
            finally
            {
                if (tab.Watcher is not null) tab.Watcher.EnableRaisingEvents = true;
            }
        }
        SetStatus(saved == 0 ? "No files to save" : $"Saved {saved} file{(saved == 1 ? "" : "s")}");
    }

    private void SaveFileAs()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        // Filter indices: 1=XML, 2=JSON, 3=XSD, 4=XSL/XSLT, 5=EDI, 6=All
        var ext = tab.FilePath is not null ? Path.GetExtension(tab.FilePath).ToLowerInvariant() : null;
        int filterIndex = ext switch
        {
            ".json" => 2,
            ".xsd" => 3,
            ".xsl" or ".xslt" => 4,
            ".edi" => 5,
            _ => tab.FileType == FileType.Json ? 2
               : tab.FileType == FileType.Edi ? 5
               : 1
        };

        var dlg = new SaveFileDialog
        {
            Title = "Save File As",
            Filter = "XML Files|*.xml|JSON Files|*.json|XSD Schema Files|*.xsd|XSL / XSLT Files|*.xsl;*.xslt|EDIFACT Files|*.edi|All Files|*.*",
            FilterIndex = filterIndex,
            FileName = tab.FilePath is null ? string.Empty : Path.GetFileName(tab.FilePath)
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            tab.Watcher?.Dispose();
            tab.Watcher = null;
            EncodingService.WriteFileWithEncoding(dlg.FileName, tab.Editor.Text, tab.Encoding);
            tab.FilePath = dlg.FileName;
            tab.IsModified = false;
            ApplyFileTypeToTab(tab, DetectFileType(dlg.FileName, tab.Editor.Text));
            UpdateTabHeader(tab);
            UpdateStatusBar(tab);
            AttachFileWatcher(tab);
            SetStatus($"Saved: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { ShowError($"Failed to save file:\n{ex.Message}"); }
    }

    // ──────────────────────────── Format / Auto-Indent ────────────────────────────
    private void FormatDocument()
    {
        var tab = ActiveTab;
        if (tab is null || string.IsNullOrWhiteSpace(tab.Editor.Text))
        { SetStatus("Nothing to format."); return; }

        try
        {
            if (tab.FileType == FileType.Xml)
            {
                ReplaceEditorText(tab, XmlService.FormatXml(tab.Editor.Text));
                SetStatus("XML auto-indented.");
            }
            else if (tab.FileType == FileType.Json)
            {
                ReplaceEditorText(tab, JsonService.FormatJson(tab.Editor.Text));
                SetStatus("JSON auto-indented.");
            }
            else if (tab.FileType == FileType.Yaml)
            {
                ReplaceEditorText(tab, YamlService.FormatYaml(tab.Editor.Text));
                SetStatus("YAML auto-formatted.");
            }
            else if (tab.FileType == FileType.Edi)
            {
                ReplaceEditorText(tab, EdifactService.FormatEdi(tab.Editor.Text));
                var ediError = EdifactService.ValidateEdi(tab.Editor.Text);
                var ediDefErrors = EdifactService.ValidateEdiDefinition(tab.Editor.Text);
                var allEdiErrors = new List<string>();
                if (ediError is not null)
                    allEdiErrors.AddRange(ediError.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                allEdiErrors.AddRange(ediDefErrors);
                if (allEdiErrors.Count > 0)
                {
                    SetStatus("EDIFACT formatted with errors.");
                    ShowMessages(ParseValidationErrors(string.Join("\n", allEdiErrors)));
                }
                else
                {
                    SetStatus("EDIFACT auto-formatted.");
                    ClearMessages();
                }
            }
            else
            {
                var text = tab.Editor.Text.TrimStart();
                if (text.StartsWith('<'))
                {
                    ReplaceEditorText(tab, XmlService.FormatXml(tab.Editor.Text));
                    ApplyFileTypeToTab(tab, FileType.Xml);
                    SetStatus("XML auto-indented.");
                }
                else if (text.StartsWith('{') || text.StartsWith('['))
                {
                    ReplaceEditorText(tab, JsonService.FormatJson(tab.Editor.Text));
                    ApplyFileTypeToTab(tab, FileType.Json);
                    SetStatus("JSON auto-indented.");
                }
                else if (EdifactService.ValidateEdi(tab.Editor.Text) is null)
                {
                    ReplaceEditorText(tab, EdifactService.FormatEdi(tab.Editor.Text));
                    ApplyFileTypeToTab(tab, FileType.Edi);
                    SetStatus("EDIFACT auto-formatted.");
                }
                else if (LooksLikeYaml(tab.Editor.Text))
                {
                    ReplaceEditorText(tab, YamlService.FormatYaml(tab.Editor.Text));
                    ApplyFileTypeToTab(tab, FileType.Yaml);
                    SetStatus("YAML auto-formatted.");
                }
                else
                {
                    SetStatus("Could not determine file type. Open an XML, JSON, EDIFACT, or YAML file first.");
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus("Auto indent failed — the document contains a syntax error.");
            ShowMessages(ParseExceptionError("Auto indent failed", ex));
        }
    }

    private static void ReplaceEditorText(EditorTab tab, string newText)
    {
        tab.Editor.Document.BeginUpdate();
        try { tab.Editor.Document.Text = newText; }
        finally { tab.Editor.Document.EndUpdate(); }
    }

    // ──────────────────────────── Minify document ────────────────────────────

    private void MinifyDocument()
    {
        var tab = ActiveTab;
        if (tab is null || string.IsNullOrWhiteSpace(tab.Editor.Text))
        { SetStatus("Nothing to minify."); return; }

        try
        {
            if (tab.FileType == FileType.Xml)
            {
                ReplaceEditorText(tab, XmlService.MinifyXml(tab.Editor.Text));
                SetStatus("XML minified.");
            }
            else if (tab.FileType == FileType.Json)
            {
                ReplaceEditorText(tab, JsonService.MinifyJson(tab.Editor.Text));
                SetStatus("JSON minified.");
            }
            else if (tab.FileType == FileType.Edi)
            {
                ReplaceEditorText(tab, EdifactService.MinifyEdi(tab.Editor.Text));
                SetStatus("EDIFACT minified.");
            }
            else
            {
                SetStatus("Minify is not supported for this file type.");
            }
        }
        catch (Exception ex)
        {
            SetStatus("Minify failed — the document contains a syntax error.");
            ShowMessages(ParseExceptionError("Minify failed", ex));
        }
    }

    private void MinifyDocument_Click(object sender, RoutedEventArgs e) => MinifyDocument();

    // ──────────────────────────── EDIFACT hover tooltips ────────────────────────────

    private string? GetEdifactTooltipContent(EditorTab tab)
    {
        if (tab.FileType != FileType.Edi) return null;

        var mousePos = System.Windows.Input.Mouse.GetPosition(tab.Editor.TextArea.TextView);
        var pos = tab.Editor.TextArea.TextView.GetPosition(mousePos);
        if (pos is null) return null;

        int lineNumber = pos.Value.Line;
        var doc = tab.Editor.Document;
        if (lineNumber < 1 || lineNumber > doc.LineCount) return null;

        var docLine = doc.GetLineByNumber(lineNumber);
        string lineText = doc.GetText(docLine).TrimEnd();
        if (string.IsNullOrEmpty(lineText)) return null;

        // Extract segment tag (2-3 uppercase letters before first +, :, or ')
        int tagEnd = 0;
        while (tagEnd < lineText.Length && char.IsLetterOrDigit(lineText[tagEnd]) && tagEnd < 4)
            tagEnd++;
        if (tagEnd < 2 || tagEnd > 3) return null;
        string tag = lineText[..tagEnd];

        // Find the EDIFACT directory from the document's UNH segment
        string? directory = GetEdifactDirectory(tab);
        if (directory is null) return null;

        var segDef = EdifactDefinitionService.LookupSegment(directory, tag);
        if (segDef is null) return null;

        // Build tooltip content
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{segDef.Tag} — {segDef.Fields.FirstOrDefault()?.Name ?? tag}");
        foreach (var field in segDef.Fields.Skip(1))
        {
            string mandatory = field.Mandatory ? " (M)" : " (C)";
            string type = field.DataType is not null ? $" [{field.DataType}{field.MaxLength}]" : "";
            sb.AppendLine($"  {field.Id}: {field.Name}{mandatory}{type}");
        }

        return sb.ToString().TrimEnd();
    }

    private string? _cachedEdifactDirectory;
    private string? _cachedEdifactDirectoryText;

    private string? GetEdifactDirectory(EditorTab tab)
    {
        string text = tab.Editor.Text;
        // Cache based on content identity
        if (text == _cachedEdifactDirectoryText)
            return _cachedEdifactDirectory;

        _cachedEdifactDirectoryText = text;
        _cachedEdifactDirectory = null;

        // Find UNH segment and extract directory
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (!trimmed.StartsWith("UNH", StringComparison.Ordinal)) continue;
            // UNH+ref+MSGTYP:D:96A:UN...
            var elements = trimmed.Split('+');
            if (elements.Length < 3) break;
            var parts = elements[2].Split(':');
            if (parts.Length >= 3)
            {
                _cachedEdifactDirectory = $"{parts[1]}{parts[2]}";
                break;
            }
        }

        return _cachedEdifactDirectory;
    }

    // ──────────────────────────── XML ↔ JSON conversion ────────────────────────────

    private void ConvertFormat()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to convert — the editor is empty."); return; }

        try
        {
            if (tab.FileType == FileType.Xml)
            {
                // XML → JSON: open result in a new tab
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(text);
                string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
                json = JsonService.FormatJson(json);
                var newTab = AddEditorTab(null, json);
                ApplyFileTypeToTab(newTab, FileType.Json);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted XML → JSON (new tab)");
            }
            else if (tab.FileType == FileType.Json)
            {
                // JSON → XML: open result in a new tab
                var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(text)
                    ?? Newtonsoft.Json.JsonConvert.DeserializeXmlNode("{\"root\":" + text + "}");
                if (xmlDoc is null) { SetStatus("Conversion failed: could not build XML document."); return; }
                string xml = XmlService.FormatXml(xmlDoc.OuterXml);
                var newTab = AddEditorTab(null, xml);
                ApplyFileTypeToTab(newTab, FileType.Xml);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted JSON → XML (new tab)");
            }
            else if (tab.FileType == FileType.Yaml)
            {
                // YAML → JSON: open result in a new tab
                string json = YamlService.ConvertYamlToJson(text);
                json = JsonService.FormatJson(json);
                var newTab = AddEditorTab(null, json);
                ApplyFileTypeToTab(newTab, FileType.Json);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted YAML → JSON (new tab)");
            }
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Conversion failed: {ex.Message}" }
            });
            SetStatus("Conversion failed — see Messages panel");
        }
    }

    // ──────────────────────────── Convert to YAML ────────────────────────────

    private void ConvertToYaml()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to convert — the editor is empty."); return; }

        try
        {
            if (tab.FileType == FileType.Xml)
            {
                // XML → YAML via JSON intermediate
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(text);
                string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);
                string yaml = YamlService.ConvertJsonToYaml(json);
                var newTab = AddEditorTab(null, yaml);
                ApplyFileTypeToTab(newTab, FileType.Yaml);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted XML → YAML (new tab)");
            }
            else if (tab.FileType == FileType.Json)
            {
                // JSON → YAML
                string yaml = YamlService.ConvertJsonToYaml(text);
                var newTab = AddEditorTab(null, yaml);
                ApplyFileTypeToTab(newTab, FileType.Yaml);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted JSON → YAML (new tab)");
            }
            else if (tab.FileType == FileType.Yaml)
            {
                // YAML → XML via JSON intermediate
                string json = YamlService.ConvertYamlToJson(text);
                var xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(json)
                    ?? Newtonsoft.Json.JsonConvert.DeserializeXmlNode("{\"root\":" + json + "}");
                if (xmlDoc is null) { SetStatus("Conversion failed: could not build XML document."); return; }
                string xml = XmlService.FormatXml(xmlDoc.OuterXml);
                var newTab = AddEditorTab(null, xml);
                ApplyFileTypeToTab(newTab, FileType.Xml);
                newTab.IsModified = true;
                RefreshTabTitle(newTab);
                ClearMessages();
                SetStatus("Converted YAML → XML (new tab)");
            }
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Conversion failed: {ex.Message}" }
            });
            SetStatus("Conversion failed — see Messages panel");
        }
    }

    private void ConvertToYaml_Click(object sender, RoutedEventArgs e) => ConvertToYaml();

    // ──────────────────────────── Generate Sample XML ────────────────────────────
    private void GenerateSampleXmlFromXsd()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to generate from — the editor is empty."); return; }

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            string? xsdFileName = tab.FilePath is not null ? Path.GetFileName(tab.FilePath) : null;
            string sampleXml = XmlService.GenerateSampleXml(text, xsdFileName);
            sampleXml = XmlService.FormatXml(sampleXml);
            var newTab = AddEditorTab(null, sampleXml);
            ApplyFileTypeToTab(newTab, FileType.Xml);
            newTab.IsModified = true;
            RefreshTabTitle(newTab);
            ClearMessages();
            SetStatus("Sample XML generated from XSD (new tab)");
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Sample XML generation failed: {ex.Message}" }
            });
            SetStatus("Sample XML generation failed — see Messages panel");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    // ──────────────────────────── Generate Sample JSON ────────────────────────────

    private void GenerateSampleJsonFromSchema()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to generate from — the editor is empty."); return; }

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            string sampleJson = JsonSchemaService.GenerateSampleJson(text);
            sampleJson = JsonService.FormatJson(sampleJson);
            var newTab = AddEditorTab(null, sampleJson);
            ApplyFileTypeToTab(newTab, FileType.Json);
            newTab.IsModified = true;
            RefreshTabTitle(newTab);
            ClearMessages();
            SetStatus("Sample JSON generated from schema (new tab)");
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Sample JSON generation failed: {ex.Message}" }
            });
            SetStatus("Sample JSON generation failed — see Messages panel");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void GenerateSampleJson_Click(object sender, RoutedEventArgs e) => GenerateSampleJsonFromSchema();

    // ──────────────────────────── XSD validation ────────────────────────────

    private static bool IsXsdTab(EditorTab tab) =>
        tab.FilePath is not null &&
        Path.GetExtension(tab.FilePath).Equals(".xsd", StringComparison.OrdinalIgnoreCase);

    private void OpenValidateXsdDialog()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        bool activeIsXsd = IsXsdTab(tab);
        bool wantXsd = !activeIsXsd;

        // Collect open tabs of the counterpart type
        var counterpartTabs = editorTabs.Items
            .OfType<TabItem>()
            .Select(ti => ti.Tag as EditorTab)
            .Where(t => t is not null && t != tab && t.FileType == FileType.Xml && IsXsdTab(t!) == wantXsd)
            .Select(t => (t!.TabTitle, t.Editor.Text))
            .ToList();

        var selector = new XsdValidationSelectorWindow(
            _isDarkMode, activeIsXsd, counterpartTabs, _lastOpenDirectory)
        {
            Owner = this,
        };

        if (selector.ShowDialog() != true) return;

        PerformXsdValidation(tab, selector.SelectedContent!);
    }

    private void BrowseAndValidateXsd(EditorTab tab, bool activeIsXsd)
    {
        bool browsingForXsd = !activeIsXsd;
        var dlg = new OpenFileDialog
        {
            Title = browsingForXsd ? "Select XSD Schema File" : "Select XML File",
            Filter = browsingForXsd
                ? "XSD Schema Files|*.xsd|All Files|*.*"
                : "XML Files|*.xml;*.xsd;*.xsl;*.xslt|All Files|*.*",
            InitialDirectory = _lastOpenDirectory ?? string.Empty,
        };
        if (dlg.ShowDialog() != true) return;

        _lastOpenDirectory = Path.GetDirectoryName(dlg.FileName);
        try
        {
            var (content, _) = EncodingService.ReadFileWithEncoding(dlg.FileName);
            PerformXsdValidation(tab, content);
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Could not read file: {ex.Message}" }
            });
            SetStatus("File read failed — see Messages panel");
        }
    }

    private void PerformXsdValidation(EditorTab activeTab, string counterpartContent)
    {
        bool activeIsXsd = IsXsdTab(activeTab);
        string xmlContent = activeIsXsd ? counterpartContent : activeTab.Editor.Text;
        string xsdContent = activeIsXsd ? activeTab.Editor.Text : counterpartContent;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            IReadOnlyList<string> errors = XmlService.ValidateXmlAgainstXsd(xmlContent, xsdContent);
            if (errors.Count == 0)
            {
                ShowMessages(new List<Models.MessageItem>
                {
                    new() { Message = "XML validation passed — no errors found.", IsSuccess = true }
                });
                SetStatus("XML validation passed — no errors found.");
            }
            else
            {
                ShowMessages(ParseValidationErrors(string.Join("\n", errors)));
                SetStatus($"Validation found {errors.Count} error(s) — see Messages panel");
            }
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"Validation failed: {ex.Message}" }
            });
            SetStatus("Validation failed — see Messages panel");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    // ──────────────────────────── Copy for Excel ────────────────────────────
    private void CopyToExcel()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        if (tab.FileType is not FileType.Xml and not FileType.Json and not FileType.Yaml)
        { SetStatus("Copy for Excel is only available for XML, JSON, and YAML files."); return; }

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to copy — the editor is empty."); return; }

        try
        {
            if (tab.FileType == FileType.Xml)
                ClipboardService.CopyXmlAsHtml(text);
            else if (tab.FileType == FileType.Yaml)
                ClipboardService.CopyYamlAsHtml(text);
            else
                ClipboardService.CopyJsonAsHtml(text);

            SetStatus("Copied for Excel — paste into a cell to see syntax-highlighted content.");
        }
        catch (Exception ex)
        {
            ShowError($"Copy for Excel failed:\n\n{ex.Message}");
        }
    }

    // ──────────────────────────── DCSA Sort ────────────────────────────

    private async Task SortDcsaAsync()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        if (tab.FileType != FileType.Json)
        { SetStatus("Sort DCSA is only available for JSON files."); return; }

        string text = tab.Editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        { SetStatus("Nothing to sort — the editor is empty."); return; }

        if (_dcsaSchemaUrls.Count == 0)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = "No DCSA schema URLs configured. Go to Settings → DCSA Schema URLs… to add URLs." }
            });
            return;
        }

        if (_toolbarButtons.TryGetValue("sortDcsaButton", out var sortBtn))
            sortBtn.IsEnabled = false;
        SetStatus("Fetching DCSA schemas…");

        try
        {
            var result = await DcsaService.SortJsonBySchemaAsync(text, _dcsaSchemaUrls);
            var formatted = JsonService.FormatJson(result.SortedJson);
            ReplaceEditorText(tab, formatted);
            ClearMessages();
            SetStatus($"Sorted as {result.SchemaName} ({result.ApiName} v{result.ApiVersion})");
        }
        catch (HttpRequestException ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"DCSA Sort failed: {ex.Message}" },
                new() { Message = "Check Settings → DCSA Schema URLs… to verify the configured URLs are correct." }
            });
            SetStatus("DCSA Sort failed — see Messages panel");
        }
        catch (Exception ex)
        {
            ShowMessages(new List<Models.MessageItem>
            {
                new() { Message = $"DCSA Sort failed: {ex.Message}" }
            });
            SetStatus("DCSA Sort failed — see Messages panel");
        }
        finally
        {
            if (ActiveTab?.FileType == FileType.Json && _toolbarButtons.TryGetValue("sortDcsaButton", out var sortBtnFinally))
                sortBtnFinally.IsEnabled = true;
        }
    }

    private void OpenDcsaSchemaUrlSettings()
    {
        var dlg = new DcsaSettingsWindow(_isDarkMode, _dcsaSchemaUrls) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } newUrls)
        {
            _dcsaSchemaUrls = newUrls;
            SetStatus("DCSA schema URLs updated");
        }
    }

    private void CustomizeToolbar()
    {
        var dlg = new ToolbarSettingsWindow(_isDarkMode, _toolbarLayout, ToolbarButtonRegistry) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } newLayout)
        {
            _toolbarLayout = newLayout;
            BuildToolbar();
            SetStatus("Toolbar layout updated");
        }
    }

    // ──────────────────────────── Context menu per editor ────────────────────────────
    private void BuildEditorContextMenu(EditorTab tab)
    {
        var findItem = new MenuItem { Header = "Find…", InputGestureText = "Ctrl+F" };
        findItem.Click += (s, e) => OpenFind();

        var copyPathItem = new MenuItem { Header = "Copy XPath / JSON Path" };
        copyPathItem.Click += (s, e) => CopyPath(tab);

        var menu = new ContextMenu();
        menu.Items.Add(findItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(copyPathItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Select All", Command = ApplicationCommands.SelectAll });

        menu.Opened += (s, e) =>
        {
            bool isXml = tab.FileType == FileType.Xml;
            bool isJson = tab.FileType == FileType.Json;
            bool isYaml = tab.FileType == FileType.Yaml;
            copyPathItem.Header = isYaml ? "Copy YAML Path" : isJson ? "Copy JSON Path" : "Copy XPath";
            copyPathItem.IsEnabled = (isXml || isJson || isYaml) && !string.IsNullOrWhiteSpace(tab.Editor.Text);
        };

        tab.Editor.PreviewMouseRightButtonDown += (s, e) =>
        {
            var textView = tab.Editor.TextArea.TextView;
            var mouseInView = e.GetPosition(textView);
            var tvPos = textView.GetPosition(mouseInView);
            if (tvPos.HasValue)
            {
                tab.RightClickLine = tvPos.Value.Line;
                tab.RightClickColumn = tvPos.Value.Column;
            }
            else
            {
                // GetPosition returns null for gutter/empty-space clicks;
                // fall back to finding the nearest visual line by Y coordinate.
                var docY = mouseInView.Y + textView.ScrollOffset.Y;
                var vLine = textView.VisualLines
                    .FirstOrDefault(vl => vl.VisualTop <= docY && docY < vl.VisualTop + vl.Height);
                tab.RightClickLine = vLine?.FirstDocumentLine.LineNumber
                                     ?? tab.Editor.TextArea.Caret.Line;
                tab.RightClickColumn = 0;
            }
        };

        tab.Editor.ContextMenu = menu;
        tab.Editor.TextArea.ContextMenu = menu;
    }

    private void CopyPath(EditorTab tab)
    {
        if (string.IsNullOrWhiteSpace(tab.Editor.Text)) return;

        try
        {
            string? path = tab.FileType == FileType.Yaml
                ? YamlService.GetYamlPathAtLine(tab.Editor.Text, tab.RightClickLine)
                : tab.FileType == FileType.Json
                ? JsonService.GetJsonPathAtLine(tab.Editor.Text, tab.RightClickLine)
                : XmlService.GetXPathAtLine(tab.Editor.Text, tab.RightClickLine, tab.RightClickColumn);

            if (path is null)
            { SetStatus("Could not determine path at cursor position."); return; }

            // SetDataObject is more robust than SetText inside WPF
            Clipboard.SetDataObject(path, true);
            SetStatus($"Copied: {path}");
        }
        catch (Exception ex) { SetStatus($"Error getting path: {ex.Message}"); }
    }

    // ──────────────────────────── XPath Tool ────────────────────────────
    private void XpathInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { ExecuteXPath(); e.Handled = true; }
    }

    private void ExecuteXPath_Click(object sender, RoutedEventArgs e) => ExecuteXPath();

    private void ExecuteXPath()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        var expression = xpathInput.Text?.Trim();
        if (string.IsNullOrEmpty(expression))
        { SetStatus("Enter an XPath expression first."); return; }

        if (tab.FileType is not FileType.Xml and not FileType.Json and not FileType.Yaml)
        { SetStatus("Please open an XML, JSON, or YAML file first."); return; }

        if (string.IsNullOrWhiteSpace(tab.Editor.Text))
        { SetStatus("The editor is empty."); return; }

        bool isJson = tab.FileType == FileType.Json;
        bool isYaml = tab.FileType == FileType.Yaml;

        // Track query in history
        if (_queryHistory.Count == 0 || !string.Equals(_queryHistory[0], expression, StringComparison.Ordinal))
            _queryHistory.Insert(0, expression);
        _queryHistoryIndex = -1;

        try
        {
            var results = isYaml
                ? YamlService.ExecuteYamlPath(tab.Editor.Text, expression)
                : isJson
                ? JsonService.ExecuteJsonPath(tab.Editor.Text, expression)
                : XmlService.ExecuteXPath(tab.Editor.Text, expression);
            xpathResultsList.ItemsSource = results;
            string kind = isYaml ? "YAMLPath" : isJson ? "JSONPath" : "XPath";
            xpathResultsHeader.Text = results.Count == 0
                ? "Results:  (no matches)"
                : $"Results:  {results.Count} node(s) found";
            SetStatus(results.Count == 0
                ? $"{kind} executed — no matching nodes."
                : $"{kind} executed — {results.Count} match(es).");
        }
        catch (Exception ex)
        {
            xpathResultsList.ItemsSource = null;
            xpathResultsHeader.Text = "Results:";
            string kind = isYaml ? "YAMLPath" : isJson ? "JSONPath" : "XPath";
            SetStatus($"{kind} error: {ex.Message}");
            MessageBox.Show(ex.Message, $"{kind} Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void XpathResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (xpathResultsList.SelectedItem is not XPathResultItem item) return;
        var tab = ActiveTab;
        if (tab is null) return;
        NavigateToLine(tab, item.LineNumber, item.XPath);
    }

    private void AllPaths_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (allPathsList.SelectedItem is not XPathResultItem item) return;
        var tab = ActiveTab;
        if (tab is null) return;
        NavigateToLine(tab, item.LineNumber, item.XPath);
    }

    private void AllPathsFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool empty = string.IsNullOrEmpty(allPathsFilter.Text);
        allPathsFilterHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        allPathsFilterClear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ApplyAllPathsFilter();
    }

    private void AllPathsFilterClear_Click(object sender, RoutedEventArgs e)
    {
        allPathsFilter.Clear();
        allPathsFilter.Focus();
    }

    private void AllPathsCopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            Clipboard.SetDataObject(path, true);
            SetStatus($"Copied: {path}");
        }
    }

    private void NavigateToLine(EditorTab tab, int line, string pathDescription)
    {
        if (line < 1 || line > tab.Editor.Document.LineCount) return;

        tab.HighlightRenderer.Line = line;
        tab.Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

        // Defer focus + selection + scroll until after the ListBox click event finishes,
        // so the editor actually has keyboard focus when Select() is called.
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                tab.Editor.Focus();
                var docLine = tab.Editor.Document.GetLineByNumber(line);
                tab.Editor.Select(docLine.Offset, docLine.Length);
                tab.Editor.ScrollTo(line, 1);
                SetStatus($"Navigated to line {line}  —  {pathDescription}");
            }
            catch { }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void PopulateAllPaths(EditorTab? tab)
    {
        if (tab is null || string.IsNullOrWhiteSpace(tab.Editor.Text)
            || tab.FileType is not FileType.Xml and not FileType.Json and not FileType.Yaml)
        {
            _allPathsFullList = [];
            allPathsList.ItemsSource = null;
            allPathsHeader.Text = "All Paths";
            return;
        }

        try
        {
            string kind;
            if (tab.FileType == FileType.Yaml)
            {
                _allPathsFullList = YamlService.GetAllPaths(tab.Editor.Text);
                kind = "YAML paths";
            }
            else if (tab.FileType == FileType.Json)
            {
                _allPathsFullList = JsonService.GetAllPaths(tab.Editor.Text);
                kind = "JSON paths";
            }
            else
            {
                _allPathsFullList = XmlService.GetAllPaths(tab.Editor.Text);
                kind = "XPath expressions";
            }
            ApplyAllPathsFilter();
            allPathsHeader.Text = $"All Paths  ({allPathsList.Items.Count}/{_allPathsFullList.Count} {kind})";
        }
        catch
        {
            _allPathsFullList = [];
            allPathsList.ItemsSource = null;
            allPathsHeader.Text = "All Paths  (parse error)";
        }
    }

    private void ApplyAllPathsFilter()
    {
        var filter = allPathsFilter.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(filter)
            ? _allPathsFullList
            : _allPathsFullList.Where(i =>
                i.XPath.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.Preview.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        allPathsList.ItemsSource = filtered;
        // Update count in header
        var headerBase = allPathsHeader.Text.Split('(')[0].TrimEnd();
        if (_allPathsFullList.Count > 0)
            allPathsHeader.Text = $"{headerBase}  ({filtered.Count}/{_allPathsFullList.Count})";
    }

    // ──────────────────────────── inline editor view toggle ────────────────────────────
    private FrameworkElement BuildTabViewContainer(EditorTab state)
    {
        var tv = CreateInlineTreeView();
        tv.Visibility = Visibility.Collapsed;
        state.InlineTreeView = tv;

        var schemaTreeSv = CreateSchemaTreeScrollViewer();
        schemaTreeSv.Visibility = Visibility.Collapsed;
        state.SchemaTreeView = schemaTreeSv;

        // Ctrl+Scroll zoom for schema tree view
        schemaTreeSv.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            ChangeEditorZoom(state, e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        };

        // Schema filter bar
        var schemaFilterBar = BuildSchemaFilterBar(state);
        schemaFilterBar.Visibility = Visibility.Collapsed;
        state.SchemaFilterBar = schemaFilterBar;

        var contentLayer = new Grid();
        contentLayer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentLayer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(schemaFilterBar, 0);
        contentLayer.Children.Add(schemaFilterBar);

        var viewLayer = new Grid();
        viewLayer.Children.Add(state.Editor);
        viewLayer.Children.Add(tv);
        viewLayer.Children.Add(schemaTreeSv);
        Grid.SetRow(viewLayer, 1);
        contentLayer.Children.Add(viewLayer);

        var textBtn = MakeViewToggleButton("Text", active: true);
        var gridBtn = MakeViewToggleButton("Grid", active: false);
        state.TextViewButton = textBtn;
        state.GridViewButton = gridBtn;

        textBtn.Click += (s, e) => SwitchToTextView(state);
        gridBtn.Click += (s, e) => SwitchToGridView(state);

        var schemaTreeBtn = MakeViewToggleButton("Schema Tree", active: false);
        schemaTreeBtn.Visibility = Visibility.Collapsed;
        state.SchemaTreeButton = schemaTreeBtn;

        schemaTreeBtn.Click += (s, e) => SwitchToSchemaTreeView(state);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
        btnPanel.Children.Add(textBtn);
        btnPanel.Children.Add(gridBtn);
        btnPanel.Children.Add(schemaTreeBtn);

        // Zoom controls
        var zoomOutBtn = MakeZoomPanelButton("−");
        var zoomLabel = new TextBlock
        {
            Width = 36,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10,
            Cursor = Cursors.Hand,
            ToolTip = "Click to reset zoom"
        };
        zoomLabel.SetResourceReference(TextBlock.ForegroundProperty, "ButtonForeground");
        var zoomInBtn = MakeZoomPanelButton("+");
        state.ZoomLabel = zoomLabel;
        UpdateEditorZoomLabel(state);

        zoomOutBtn.Click += (s, e) => ChangeEditorZoom(state, -1);
        zoomInBtn.Click += (s, e) => ChangeEditorZoom(state, +1);
        zoomLabel.MouseLeftButtonDown += (s, e) => ResetEditorZoom(state);

        var zoomPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        zoomPanel.Children.Add(zoomOutBtn);
        zoomPanel.Children.Add(zoomLabel);
        zoomPanel.Children.Add(zoomInBtn);

        var barDockPanel = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(btnPanel, Dock.Left);
        DockPanel.SetDock(zoomPanel, Dock.Right);
        barDockPanel.Children.Add(btnPanel);
        barDockPanel.Children.Add(zoomPanel);

        var bottomBar = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(2, 2, 0, 2),
            Child = barDockPanel
        };
        bottomBar.SetResourceReference(Border.BackgroundProperty, "PanelBackground");
        bottomBar.SetResourceReference(Border.BorderBrushProperty, "SeparatorColor");

        var container = new Grid();
        container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(contentLayer, 0);
        Grid.SetRow(bottomBar, 1);
        container.Children.Add(contentLayer);
        container.Children.Add(bottomBar);
        return container;
    }

    private TreeView CreateInlineTreeView()
    {
        var tv = new TreeView { BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = EditorFontSizeDefault };
        VirtualizingPanel.SetIsVirtualizing(tv, true);
        VirtualizingPanel.SetVirtualizationMode(tv, VirtualizationMode.Recycling);

        tv.Resources[SystemColors.HighlightBrushKey] = FindResource("TreeItemSelected");
        tv.Resources[SystemColors.HighlightTextBrushKey] = FindResource("TabActiveForeground");
        tv.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = FindResource("TreeItemInactiveSelected");
        tv.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = FindResource("ResultsForeground");

        // Foreground inherits from TreeView, which is bound to the theme resource
        tv.SetResourceReference(Control.ForegroundProperty, "GridViewForeground");

        const string itemStyleXaml = """
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                   TargetType="TreeViewItem">
                <Setter Property="IsExpanded" Value="{Binding IsExpanded}"/>
                <Setter Property="Padding"    Value="2,1"/>
            </Style>
            """;
        tv.ItemContainerStyle = (Style)XamlReader.Parse(itemStyleXaml);

        const string templateXaml = """
            <HierarchicalDataTemplate
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                ItemsSource="{Binding Children}">
                <StackPanel Orientation="Horizontal" Margin="0,1">
                    <TextBlock Text="{Binding Label}"        Foreground="{Binding LabelColor}"
                               FontWeight="SemiBold" FontFamily="Consolas"/>
                    <TextBlock Text="{Binding ValueDisplay}" Foreground="{Binding ValueColor}"
                               Margin="8,0,0,0" FontFamily="Consolas"/>
                </StackPanel>
            </HierarchicalDataTemplate>
            """;
        tv.ItemTemplate = (HierarchicalDataTemplate)XamlReader.Parse(templateXaml);
        return tv;
    }

    private static Button MakeViewToggleButton(string label, bool active)
    {
        var btn = new Button
        {
            Content = label,
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI"),
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(0, 0, 2, 0),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        SetViewButtonActive(btn, active);
        return btn;
    }

    private static Button MakeZoomPanelButton(string symbol)
    {
        var btn = new Button
        {
            Content = symbol,
            FontSize = 13,
            Width = 22,
            Height = 18,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 1, 0),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        btn.SetResourceReference(Control.BackgroundProperty, "PanelBackground");
        btn.SetResourceReference(Control.ForegroundProperty, "ButtonForeground");
        return btn;
    }

    private static void SetViewButtonActive(Button btn, bool active)
    {
        if (active)
        {
            btn.SetResourceReference(Control.BackgroundProperty, "AccentBlue");
            btn.SetResourceReference(Control.ForegroundProperty, "TabActiveForeground");
        }
        else
        {
            btn.SetResourceReference(Control.BackgroundProperty, "PanelBackground");
            btn.SetResourceReference(Control.ForegroundProperty, "ButtonForeground");
        }
    }

    private void SwitchToTextView(EditorTab tab)
    {
        tab.IsGridMode = false;
        tab.IsSchemaTreeMode = false;
        tab.Editor.Visibility = Visibility.Visible;
        if (tab.InlineTreeView is not null) tab.InlineTreeView.Visibility = Visibility.Collapsed;
        if (tab.SchemaTreeView is not null) tab.SchemaTreeView.Visibility = Visibility.Collapsed;
        if (tab.SchemaFilterBar is not null) tab.SchemaFilterBar.Visibility = Visibility.Collapsed;
        SetAllViewButtons(tab, textActive: true);
    }

    private void SwitchToGridView(EditorTab tab)
    {
        if (tab.InlineTreeView is null) return;
        bool ok = tab.FileType is FileType.Xml or FileType.Json or FileType.Yaml;
        if (!ok) { SwitchToTextView(tab); return; }

        try
        {
            tab.InlineTreeView.ItemsSource = tab.FileType switch
            {
                FileType.Xml => BuildXmlGrid(tab.Editor.Text, _isDarkMode),
                FileType.Json => BuildJsonGrid(tab.Editor.Text, _isDarkMode),
                FileType.Yaml => BuildYamlGrid(tab.Editor.Text, _isDarkMode),
                _ => null
            };
        }
        catch { tab.InlineTreeView.ItemsSource = null; }

        tab.IsGridMode = true;
        tab.IsSchemaTreeMode = false;
        tab.Editor.Visibility = Visibility.Collapsed;
        tab.InlineTreeView.Visibility = Visibility.Visible;
        if (tab.SchemaTreeView is not null) tab.SchemaTreeView.Visibility = Visibility.Collapsed;
        if (tab.SchemaFilterBar is not null) tab.SchemaFilterBar.Visibility = Visibility.Collapsed;
        SetAllViewButtons(tab, gridActive: true);
    }

    // ──────────────────────────── schema views ────────────────────────────

    private void SwitchToSchemaTreeView(EditorTab tab)
    {
        if (tab.SchemaTreeView is null) return;
        var nodes = GetOrParseSchemaNodes(tab);
        if (nodes is null) { SwitchToTextView(tab); return; }

        tab.IsGridMode = false;
        tab.IsSchemaTreeMode = true;
        tab.Editor.Visibility = Visibility.Collapsed;
        if (tab.InlineTreeView is not null) tab.InlineTreeView.Visibility = Visibility.Collapsed;
        tab.SchemaTreeView.Visibility = Visibility.Visible;
        if (tab.SchemaFilterBar is not null) tab.SchemaFilterBar.Visibility = Visibility.Visible;
        SetAllViewButtons(tab, schemaTreeActive: true);
        ApplySchemaFilter(tab);

        // Show schema statistics in the status bar
        if (tab.FileType == FileType.Xml && tab.FilePath is not null
            && Path.GetExtension(tab.FilePath).Equals(".xsd", StringComparison.OrdinalIgnoreCase))
        {
            var (elements, complexTypes, simpleTypes) = XsdSchemaService.GetStatistics(nodes);
            SetStatus($"Schema: {elements} elements, {complexTypes} complex types, {simpleTypes} simple types");
        }
        else if (tab.IsSchemaDetected)
        {
            var (properties, objects, arrays) = JsonSchemaService.GetStatistics(nodes);
            SetStatus($"Schema: {properties} properties, {objects} objects, {arrays} arrays");
        }
    }

    private static void SetAllViewButtons(EditorTab tab,
        bool textActive = false, bool gridActive = false,
        bool schemaTreeActive = false)
    {
        if (tab.TextViewButton is not null) SetViewButtonActive(tab.TextViewButton, textActive);
        if (tab.GridViewButton is not null) SetViewButtonActive(tab.GridViewButton, gridActive);
        if (tab.SchemaTreeButton is not null) SetViewButtonActive(tab.SchemaTreeButton, schemaTreeActive);
    }

    private static List<Models.SchemaNode>? GetOrParseSchemaNodes(EditorTab tab)
    {
        if (tab.CachedSchemaNodes is not null) return tab.CachedSchemaNodes;

        try
        {
            var content = tab.Editor.Text;
            if (IsXsdTab(tab))
            {
                tab.CachedSchemaNodes = XsdSchemaService.ParseXsdSchema(content);
            }
            else if (JsonSchemaService.IsJsonSchemaContent(content))
            {
                tab.CachedSchemaNodes = JsonSchemaService.ParseJsonSchema(content);
            }
        }
        catch { /* ignore parse errors */ }

        return tab.CachedSchemaNodes;
    }

    private static bool DetectSchemaContent(EditorTab tab)
    {
        if (IsXsdTab(tab)) return true;
        if (tab.FileType is FileType.Json or FileType.Yaml or FileType.None)
        {
            try { return JsonSchemaService.IsJsonSchemaContent(tab.Editor.Text); }
            catch { return false; }
        }
        return false;
    }

    private static void UpdateSchemaButtonVisibility(EditorTab tab)
    {
        bool show = tab.IsSchemaDetected;
        var vis = show ? Visibility.Visible : Visibility.Collapsed;
        if (tab.SchemaTreeButton is not null) tab.SchemaTreeButton.Visibility = vis;
    }

    private static List<string> BuildSchemaRestrictionLines(Models.SchemaNode node)
    {
        var lines = new List<string>();
        // XSD restrictions
        if (node.Restrictions.TryGetValue("enumeration", out var enums))
            lines.Add($"Values: {enums}");
        if (node.Restrictions.TryGetValue("pattern", out var pat))
            lines.Add($"Pattern: {pat}");
        if (node.Restrictions.TryGetValue("length", out var len))
            lines.Add($"Length: {len}");
        if (node.Restrictions.TryGetValue("maxLength", out var maxLen))
            lines.Add($"Max Length: {maxLen}");
        if (node.Restrictions.TryGetValue("minLength", out var minLen))
            lines.Add($"Min Length: {minLen}");
        if (node.Restrictions.TryGetValue("minInclusive", out var minInc))
            lines.Add($"Min: {minInc}");
        if (node.Restrictions.TryGetValue("maxInclusive", out var maxInc))
            lines.Add($"Max: {maxInc}");
        if (node.Restrictions.TryGetValue("minExclusive", out var minExc))
            lines.Add($"Min (exclusive): {minExc}");
        if (node.Restrictions.TryGetValue("maxExclusive", out var maxExc))
            lines.Add($"Max (exclusive): {maxExc}");
        if (node.Restrictions.TryGetValue("totalDigits", out var td))
            lines.Add($"Total Digits: {td}");
        if (node.Restrictions.TryGetValue("fractionDigits", out var fd))
            lines.Add($"Fraction Digits: {fd}");
        if (node.Restrictions.TryGetValue("whiteSpace", out var ws))
            lines.Add($"WhiteSpace: {ws}");
        if (node.Restrictions.TryGetValue("union", out var union))
            lines.Add($"Union: {union}");
        if (node.Restrictions.TryGetValue("list", out var list))
            lines.Add($"List: {list}");
        // JSON Schema restrictions
        if (node.Restrictions.TryGetValue("minimum", out var minimum))
            lines.Add($"Minimum: {minimum}");
        if (node.Restrictions.TryGetValue("maximum", out var maximum))
            lines.Add($"Maximum: {maximum}");
        if (node.Restrictions.TryGetValue("exclusiveMinimum", out var exMin))
            lines.Add($"Exclusive Minimum: {exMin}");
        if (node.Restrictions.TryGetValue("exclusiveMaximum", out var exMax))
            lines.Add($"Exclusive Maximum: {exMax}");
        if (node.Restrictions.TryGetValue("format", out var fmt))
            lines.Add($"Format: {fmt}");
        if (node.Restrictions.TryGetValue("minItems", out var minIt))
            lines.Add($"Min Items: {minIt}");
        if (node.Restrictions.TryGetValue("maxItems", out var maxIt))
            lines.Add($"Max Items: {maxIt}");
        if (node.Restrictions.TryGetValue("multipleOf", out var multOf))
            lines.Add($"Multiple Of: {multOf}");
        if (node.Restrictions.TryGetValue("minProperties", out var minPr))
            lines.Add($"Min Properties: {minPr}");
        if (node.Restrictions.TryGetValue("maxProperties", out var maxPr))
            lines.Add($"Max Properties: {maxPr}");
        // Format from SchemaNode.Format property (JSON Schema)
        if (node.Format is not null && !node.Restrictions.ContainsKey("format"))
            lines.Add($"Format: {node.Format}");
        return lines;
    }

    private Border BuildSchemaFilterBar(EditorTab tab)
    {
        var filterBox = new TextBox
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(5, 4, 22, 4),
            ToolTip = "Filter by name, type, or documentation (case-insensitive)"
        };
        filterBox.SetResourceReference(Control.BackgroundProperty, "EditorBackground");
        filterBox.SetResourceReference(Control.ForegroundProperty, "EditorForeground");
        filterBox.SetResourceReference(Control.BorderBrushProperty, "SeparatorColor");
        tab.SchemaFilterBox = filterBox;

        var hint = new TextBlock
        {
            Text = "Filter schema…",
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 0, 0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "PlaceholderForeground");

        var clearBtn = new Button
        {
            Content = "✕",
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 3, 0),
            Padding = new Thickness(0),
            FontSize = 10,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Arrow,
            ToolTip = "Clear filter"
        };
        clearBtn.SetResourceReference(Control.ForegroundProperty, "PlaceholderForeground");

        filterBox.TextChanged += (_, _) =>
        {
            hint.Visibility = string.IsNullOrEmpty(filterBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            clearBtn.Visibility = string.IsNullOrEmpty(filterBox.Text) ? Visibility.Collapsed : Visibility.Visible;
            if (string.IsNullOrEmpty(filterBox.Text))
            {
                _schemaFilterDebounceTimer?.Stop();
                ApplySchemaFilter(tab);
            }
            else
            {
                _schemaFilterDebounceTimer?.Stop();
                _schemaFilterDebounceTimer?.Start();
            }
        };

        clearBtn.Click += (_, _) =>
        {
            filterBox.Text = "";
            filterBox.Focus();
        };

        var grid = new Grid();
        grid.Children.Add(filterBox);
        grid.Children.Add(hint);
        grid.Children.Add(clearBtn);

        var bar = new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
        bar.SetResourceReference(Border.BorderBrushProperty, "SeparatorColor");

        return bar;
    }

    private void ApplySchemaFilter(EditorTab tab)
    {
        var nodes = tab.CachedSchemaNodes;
        if (nodes is null || !tab.IsSchemaTreeMode || tab.SchemaTreeView is null) return;

        string filter = tab.SchemaFilterBox?.Text?.Trim() ?? "";

        if (filter.Length == 0)
        {
            if (tab.CachedSchemaPanel is null)
                tab.CachedSchemaPanel = BuildSchemaTreePanel(nodes, _isDarkMode);
            tab.SchemaTreeView.Content = tab.CachedSchemaPanel;
        }
        else
        {
            var filtered = FilterSchemaNodes(nodes, filter);
            if (CountSchemaNodes(filtered) > 200)
                filtered = TruncateSchemaNodes(filtered, 200);
            tab.SchemaTreeView.Content = BuildSchemaTreePanel(filtered, _isDarkMode);
        }
    }

    private static List<Models.SchemaNode> FilterSchemaNodes(List<Models.SchemaNode> nodes, string filter)
    {
        var result = new List<Models.SchemaNode>();
        foreach (var node in nodes)
        {
            if (NodeMatchesFilter(node, filter))
            {
                // Node itself matches — include it with all children
                result.Add(node);
            }
            else
            {
                // Check if any descendants match
                var filteredChildren = FilterSchemaNodes(node.Children, filter);
                if (filteredChildren.Count > 0)
                {
                    // Create a shallow copy with only matching descendants
                    var copy = new Models.SchemaNode
                    {
                        Name = node.Name,
                        TypeName = node.TypeName,
                        TypeKind = node.TypeKind,
                        MinOccurs = node.MinOccurs,
                        MaxOccurs = node.MaxOccurs,
                        IsRequired = node.IsRequired,
                        IsChoice = node.IsChoice,
                        ChoiceGroup = node.ChoiceGroup,
                        ChoiceOption = node.ChoiceOption,
                        ChoiceKeyword = node.ChoiceKeyword,
                        IsRecursive = node.IsRecursive,
                        IsTruncated = node.IsTruncated,
                        IsAttribute = node.IsAttribute,
                        IsArrayItem = node.IsArrayItem,
                        Documentation = node.Documentation,
                        Restrictions = node.Restrictions,
                        Format = node.Format,
                        IsExpanded = true
                    };
                    copy.Children.AddRange(filteredChildren);
                    result.Add(copy);
                }
            }
        }
        return result;
    }

    private static bool NodeMatchesFilter(Models.SchemaNode node, string filter)
    {
        return node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || node.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (node.Documentation?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || node.Restrictions.Values.Any(v => v.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountSchemaNodes(List<Models.SchemaNode> nodes)
    {
        int count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountSchemaNodes(node.Children);
        }
        return count;
    }

    private static List<Models.SchemaNode> TruncateSchemaNodes(List<Models.SchemaNode> nodes, int max)
    {
        var result = new List<Models.SchemaNode>();
        int remaining = max;
        foreach (var node in nodes)
        {
            if (remaining <= 0) break;
            int nodeCount = 1 + CountSchemaNodes(node.Children);
            if (nodeCount <= remaining)
            {
                result.Add(node);
                remaining -= nodeCount;
            }
            else
            {
                // Include the node but truncate its children
                var copy = new Models.SchemaNode
                {
                    Name = node.Name,
                    TypeName = node.TypeName,
                    TypeKind = node.TypeKind,
                    MinOccurs = node.MinOccurs,
                    MaxOccurs = node.MaxOccurs,
                    IsRequired = node.IsRequired,
                    IsChoice = node.IsChoice,
                    ChoiceGroup = node.ChoiceGroup,
                    ChoiceOption = node.ChoiceOption,
                    ChoiceKeyword = node.ChoiceKeyword,
                    IsRecursive = node.IsRecursive,
                    IsTruncated = true,
                    IsAttribute = node.IsAttribute,
                    IsArrayItem = node.IsArrayItem,
                    Documentation = node.Documentation,
                    Restrictions = node.Restrictions,
                    Format = node.Format,
                    IsExpanded = node.IsExpanded
                };
                copy.Children.AddRange(TruncateSchemaNodes(node.Children, remaining - 1));
                result.Add(copy);
                break;
            }
        }
        return result;
    }

    private static T? FindChildByName<T>(DependencyObject parent, string? name, int skip = 0) where T : FrameworkElement
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        int found = 0;
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match && (name is null || match.Name == name))
            {
                if (found == skip) return match;
                found++;
            }
            var result = FindChildByName<T>(child, name, skip - found);
            if (result is not null) return result;
        }
        return null;
    }

    private static ScrollViewer CreateSchemaTreeScrollViewer()
    {
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };
    }

    // ──────────────────────────── schema tree (horizontal) ────────────────────────────

    private static FrameworkElement BuildSchemaTreePanel(List<Models.SchemaNode> roots, bool dark)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        foreach (var root in roots)
        {
            panel.Children.Add(BuildSchemaTreeNode(root, dark, 0));
        }

        return panel;
    }

    private static FrameworkElement BuildSchemaTreeNode(Models.SchemaNode node, bool dark, int depth)
    {
        // Node box
        var nodeBox = CreateSchemaNodeBox(node, dark);

        if (node.Children.Count == 0 || node.IsRecursive || depth > 30)
            return nodeBox;

        bool startCollapsed = !node.IsExpanded;

        // Children container
        var childrenPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
        };

        foreach (var child in node.Children)
        {
            var childElement = BuildSchemaTreeNode(child, dark, depth + 1);

            // Add a connecting line
            var lineAndChild = new Grid();
            lineAndChild.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            lineAndChild.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var connector = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 14, -1, 0)
            };
            connector.SetResourceReference(Border.BackgroundProperty, "SchemaTreeConnectorLine");

            Grid.SetColumn(connector, 0);
            Grid.SetColumn(childElement, 1);
            lineAndChild.Children.Add(connector);
            lineAndChild.Children.Add(childElement);

            childrenPanel.Children.Add(lineAndChild);
        }

        // Vertical line container — aligned to the left edge of childrenPanel
        var verticalLine = new Border
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        };
        verticalLine.SetResourceReference(Border.BackgroundProperty, "SchemaTreeConnectorLine");

        var subtreeGrid = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        subtreeGrid.Children.Add(verticalLine);
        subtreeGrid.Children.Add(childrenPanel);
        if (startCollapsed) subtreeGrid.Visibility = Visibility.Collapsed;

        // Collapse/expand toggle button
        var toggleBtn = new Button
        {
            Content = startCollapsed ? "▶" : "▼",
            Width = 20,
            Height = 20,
            FontSize = 10,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        toggleBtn.SetResourceReference(Control.ForegroundProperty, "LabelForeground");

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        headerPanel.Children.Add(toggleBtn);
        headerPanel.Children.Add(nodeBox);

        // Wire up collapse/expand
        var capturedSubtree = subtreeGrid;
        toggleBtn.Click += (_, _) =>
        {
            if (capturedSubtree.Visibility == Visibility.Visible)
            {
                capturedSubtree.Visibility = Visibility.Collapsed;
                toggleBtn.Content = "▶";
            }
            else
            {
                capturedSubtree.Visibility = Visibility.Visible;
                toggleBtn.Content = "▼";
            }
        };

        var treeLayout = new StackPanel { Orientation = Orientation.Vertical };
        treeLayout.Children.Add(headerPanel);
        treeLayout.Children.Add(capturedSubtree);

        return treeLayout;
    }

    private static Border CreateSchemaNodeBox(Models.SchemaNode node, bool dark)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };

        // Row 1: Name + Type (horizontal)
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var nameLabel = new TextBlock
        {
            Text = node.IsAttribute ? $"@{node.Name}" : node.Name,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (node.IsAttribute)
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "SchemaAttributeColor");
        else if (node.TypeKind is "complex" or "object")
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "SchemaComplexColor");
        else
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "SchemaSimpleColor");

        headerPanel.Children.Add(nameLabel);

        // Type label (inline next to name)
        if (node.TypeName.Length > 0)
        {
            var typeLabel = new TextBlock
            {
                Text = node.TypeName,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            typeLabel.SetResourceReference(TextBlock.ForegroundProperty, "SchemaTypeLabelColor");
            headerPanel.Children.Add(typeLabel);
        }

        panel.Children.Add(headerPanel);

        // Badges
        var badgePanel = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };

        // Type kind badge (complex/simple/array)
        string typeKindLabel = node.TypeKind switch
        {
            "complex" or "object" => "complex",
            "simple" or "string" or "number" or "integer" or "boolean" => "simple",
            "array" => "array",
            _ => ""
        };
        string typeKindBrush = typeKindLabel switch
        {
            "complex" => "SchemaChoiceBadge",
            "simple" => "SchemaSimpleBadge",
            "array" => "SchemaSimpleBadge",
            _ => ""
        };
        if (typeKindLabel.Length > 0)
            badgePanel.Children.Add(MakeSchemaBadge(typeKindLabel, typeKindBrush));

        if (node.IsRequired)
            badgePanel.Children.Add(MakeSchemaBadge("Required", "SchemaRequiredBadge"));
        else if (node.MinOccurs == "0")
            badgePanel.Children.Add(MakeSchemaBadge("Optional", "SchemaOptionalBadge"));

        if (node.IsChoice)
            badgePanel.Children.Add(MakeSchemaBadge(node.ChoiceKeyword ?? "Choice", "SchemaChoiceBadge"));

        if (node.IsRecursive)
            badgePanel.Children.Add(MakeSchemaBadge("Recursive", "SchemaRecursiveBadge"));

        if (node.MaxOccurs != "1")
            badgePanel.Children.Add(MakeSchemaBadge($"[{node.MinOccurs}..{node.MaxOccurs}]", "SchemaTypeLabelColor"));

        if (badgePanel.Children.Count > 0)
            panel.Children.Add(badgePanel);

        // Documentation
        if (node.Documentation is not null)
        {
            var docLabel = new TextBlock
            {
                Text = node.Documentation.Length > 80
                    ? node.Documentation[..80] + "…"
                    : node.Documentation,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320
            };
            docLabel.SetResourceReference(TextBlock.ForegroundProperty, "SchemaDocColor");
            panel.Children.Add(docLabel);
        }

        // Restrictions — individual lines with colored left border
        var restrictionLines = BuildSchemaRestrictionLines(node);
        if (restrictionLines.Count > 0)
        {
            foreach (var line in restrictionLines)
            {
                var lineBorder = new Border
                {
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(6, 1, 0, 1),
                    Margin = new Thickness(0, 1, 0, 0)
                };
                lineBorder.SetResourceReference(Border.BorderBrushProperty, "SchemaRestrictionColor");

                var lineText = new TextBlock
                {
                    Text = line,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas")
                };
                lineText.SetResourceReference(TextBlock.ForegroundProperty, "SchemaRestrictionColor");
                lineBorder.Child = lineText;
                panel.Children.Add(lineBorder);
            }
        }

        var border = new Border
        {
            Child = panel,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(node.MinOccurs == "0" ? 1 : 2),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 2, 0, 2),
            MinWidth = 160,
            MaxWidth = 360
        };
        border.SetResourceReference(Border.BorderBrushProperty, "SchemaTreeNodeBorder");
        border.SetResourceReference(Border.BackgroundProperty, "SchemaTreeNodeBackground");

        if (node.MinOccurs == "0")
        {
            border.BorderThickness = new Thickness(1);
        }

        return border;
    }

    private static Border MakeSchemaBadge(string text, string brushKey)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        var badge = new Border
        {
            Child = tb,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 4, 0)
        };
        badge.SetResourceReference(Border.BackgroundProperty, brushKey);
        return badge;
    }

    private static List<GridNode> BuildXmlGrid(string xmlText, bool dark)
    {
        try
        {
            var doc = XDocument.Parse(xmlText);
            if (doc.Root is null) return [];
            return [BuildXmlElement(doc.Root, 0, dark)];
        }
        catch (Exception ex)
        {
            return [new GridNode { Label = "Parse error", ValueDisplay = ex.Message, LabelColor = dark ? "#F48771" : "#C0392B" }];
        }
    }

    private static GridNode BuildXmlElement(XElement el, int depth, bool dark)
    {
        bool hasChildElements = el.HasElements;
        string leafValue = !hasChildElements && !string.IsNullOrWhiteSpace(el.Value)
            ? Truncate(el.Value.Trim()) : "";

        var node = new GridNode
        {
            Label = el.Name.LocalName,
            LabelColor = dark ? "#4EC9B0" : "#1A7070",
            ValueDisplay = leafValue,
            ValueColor = dark ? "#CE9178" : "#A84300",
            IsExpanded = depth <= 1
        };

        foreach (var attr in el.Attributes())
            node.Children.Add(new GridNode
            {
                Label = $"@{attr.Name.LocalName}",
                LabelColor = dark ? "#9CDCFE" : "#0D62B5",
                ValueDisplay = Truncate(attr.Value),
                ValueColor = dark ? "#CE9178" : "#A84300",
                IsExpanded = false
            });

        foreach (var child in el.Elements())
            node.Children.Add(BuildXmlElement(child, depth + 1, dark));

        return node;
    }

    private static List<GridNode> BuildJsonGrid(string jsonText, bool dark)
    {
        try
        {
            var root = JToken.Parse(jsonText);
            string rootLabel = root.Type == JTokenType.Array ? $"[{((JArray)root).Count}]"
                             : root.Type == JTokenType.Object ? "{}"
                             : "value";
            return [BuildJsonToken(rootLabel, root, 0, dark)];
        }
        catch (Exception ex)
        {
            return [new GridNode { Label = "Parse error", ValueDisplay = ex.Message, LabelColor = dark ? "#F48771" : "#C0392B" }];
        }
    }

    private static GridNode BuildJsonToken(string name, JToken token, int depth, bool dark)
    {
        if (token is JObject obj)
        {
            var node = new GridNode { Label = name, LabelColor = dark ? "#4EC9B0" : "#1A7070", ValueDisplay = $"{{{obj.Count}}}", IsExpanded = depth <= 1 };
            foreach (var prop in obj.Properties())
                node.Children.Add(BuildJsonToken(prop.Name, prop.Value, depth + 1, dark));
            return node;
        }
        if (token is JArray arr)
        {
            var node = new GridNode { Label = name, LabelColor = dark ? "#DCDCAA" : "#6B5C00", ValueDisplay = $"[{arr.Count}]", IsExpanded = depth <= 1 };
            for (int i = 0; i < arr.Count; i++)
                node.Children.Add(BuildJsonToken($"[{i}]", arr[i], depth + 1, dark));
            return node;
        }
        return new GridNode
        {
            Label = name,
            LabelColor = dark ? "#9CDCFE" : "#0D62B5",
            ValueDisplay = Truncate(token.ToString()),
            ValueColor = token.Type switch
            {
                JTokenType.String => dark ? "#CE9178" : "#A84300",
                JTokenType.Null => dark ? "#808080" : "#606060",
                JTokenType.Boolean => dark ? "#569CD6" : "#0D62B5",
                _ => dark ? "#B5CEA8" : "#267326"
            },
            IsExpanded = false
        };
    }

    private static string Truncate(string s, int max = 200)
        => s.Length <= max ? s : s[..max] + "…";

    private static List<GridNode> BuildYamlGrid(string yamlText, bool dark)
    {
        try
        {
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(yamlText));
            if (stream.Documents.Count == 0)
                return [new GridNode { Label = "(empty)", LabelColor = dark ? "#808080" : "#606060" }];

            var roots = new List<GridNode>();
            foreach (var doc in stream.Documents)
            {
                string docLabel = stream.Documents.Count > 1 ? $"document[{roots.Count}]" : "root";
                roots.Add(BuildYamlToken(docLabel, doc.RootNode, 0, dark));
            }
            return roots;
        }
        catch (Exception ex)
        {
            return [new GridNode { Label = "Parse error", ValueDisplay = ex.Message, LabelColor = dark ? "#F48771" : "#C0392B" }];
        }
    }

    private static GridNode BuildYamlToken(string name, YamlDotNet.RepresentationModel.YamlNode node, int depth, bool dark)
    {
        if (node is YamlDotNet.RepresentationModel.YamlMappingNode map)
        {
            var gn = new GridNode { Label = name, LabelColor = dark ? "#4EC9B0" : "#1A7070", ValueDisplay = $"{{{map.Children.Count}}}", IsExpanded = depth <= 1 };
            foreach (var kv in map.Children)
            {
                string key = kv.Key is YamlDotNet.RepresentationModel.YamlScalarNode sk ? sk.Value ?? "" : kv.Key.ToString();
                gn.Children.Add(BuildYamlToken(key, kv.Value, depth + 1, dark));
            }
            return gn;
        }
        if (node is YamlDotNet.RepresentationModel.YamlSequenceNode seq)
        {
            var gn = new GridNode { Label = name, LabelColor = dark ? "#DCDCAA" : "#6B5C00", ValueDisplay = $"[{seq.Children.Count}]", IsExpanded = depth <= 1 };
            for (int i = 0; i < seq.Children.Count; i++)
                gn.Children.Add(BuildYamlToken($"[{i}]", seq.Children[i], depth + 1, dark));
            return gn;
        }
        if (node is YamlDotNet.RepresentationModel.YamlScalarNode scalar)
        {
            return new GridNode
            {
                Label = name,
                LabelColor = dark ? "#9CDCFE" : "#0D62B5",
                ValueDisplay = Truncate(scalar.Value ?? "~"),
                ValueColor = scalar.Value is null ? (dark ? "#808080" : "#606060")
                    : scalar.Style == YamlDotNet.Core.ScalarStyle.Plain && (scalar.Value is "true" or "false")
                        ? (dark ? "#569CD6" : "#0D62B5")
                    : scalar.Style == YamlDotNet.Core.ScalarStyle.Plain && double.TryParse(scalar.Value, out _)
                        ? (dark ? "#B5CEA8" : "#267326")
                    : (dark ? "#CE9178" : "#A84300"),
                IsExpanded = false
            };
        }
        return new GridNode { Label = name, ValueDisplay = node.ToString(), LabelColor = dark ? "#808080" : "#606060" };
    }

    // ──────────────────────────── helpers ────────────────────────────
    private static FileType DetectContentFileType(string content)
    {
        var t = content.TrimStart();
        if (t.StartsWith('<')) return FileType.Xml;
        if (t.StartsWith('{') || t.StartsWith('[')) return FileType.Json;
        if (LooksLikeEdifact(t)) return FileType.Edi;
        if (LooksLikeYaml(t)) return FileType.Yaml;
        return FileType.None;
    }

    /// <summary>
    /// Fast structural heuristic: checks whether the content looks like EDIFACT
    /// without running full validation.  Accepts partial/incomplete messages.
    /// </summary>
    private static bool LooksLikeEdifact(string t)
    {
        if (t.Length == 0) return false;
        // UNA service string advice is a clear indicator
        if (t.StartsWith("UNA", StringComparison.Ordinal)) return true;
        // Check that the first token is a valid 2-3 uppercase-letter tag
        // followed immediately by a segment separator (+), component separator (:),
        // or segment terminator (')
        int tagLen = 0;
        while (tagLen < t.Length && char.IsUpper(t[tagLen])) tagLen++;
        if (tagLen is < 2 or > 3) return false;
        if (tagLen >= t.Length) return false;
        char next = t[tagLen];
        return next is '+' or ':' or '\'';
    }

    /// <summary>
    /// Heuristic: checks whether content looks like YAML.
    /// Matches document markers (---) or key: value patterns.
    /// </summary>
    private static bool LooksLikeYaml(string t)
    {
        if (t.Length == 0) return false;
        if (t.StartsWith("---")) return true;
        // Check for key: value pattern on the first non-comment line
        foreach (var rawLine in t.Split('\n', 5))
        {
            var line = rawLine.TrimEnd('\r').TrimStart();
            if (line.Length == 0 || line[0] == '#') continue;
            // Look for a word followed by : then space or end-of-line
            int colon = line.IndexOf(':');
            if (colon > 0 && (colon + 1 >= line.Length || line[colon + 1] == ' '))
            {
                // Verify the part before colon looks like a key (alphanumeric/underscore/dash)
                var key = line[..colon].TrimEnd();
                if (key.Length > 0 && !key.Contains(' '))
                    return true;
            }
            break; // Only check the first non-comment line
        }
        return false;
    }

    private static FileType DetectFileType(string filePath, string content)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".xml" or ".xsd" or ".xsl" or ".xslt") return FileType.Xml;
        if (ext == ".json") return FileType.Json;
        if (ext == ".edi") return FileType.Edi;
        if (ext is ".yaml" or ".yml") return FileType.Yaml;

        return DetectContentFileType(content);
    }

    private void ApplyFileTypeToTab(EditorTab tab, FileType type)
    {
        tab.FileType = type;
        tab.Editor.SyntaxHighlighting = type switch
        {
            FileType.Xml => GetOrCreateXmlHighlighting(_isDarkMode),
            FileType.Json => GetOrCreateJsonHighlighting(_isDarkMode),
            FileType.Edi => GetOrCreateEdiHighlighting(_isDarkMode),
            FileType.Yaml => GetOrCreateYamlHighlighting(_isDarkMode),
            _ => null
        };
        UpdateFolding(tab);
        UpdateRightPanelForTab(tab);
        UpdateStatusBar(tab);
        tab.CachedSchemaNodes = null;
        tab.CachedSchemaPanel = null;
        tab.IsSchemaDetected = DetectSchemaContent(tab);
        UpdateSchemaButtonVisibility(tab);
    }

    private static void UpdateFolding(EditorTab tab)
    {
        if (tab.FileType is FileType.Xml or FileType.Json or FileType.Edi or FileType.Yaml)
        {
            tab.FoldingManager ??= FoldingManager.Install(tab.Editor.TextArea);
            try
            {
                if (tab.FileType == FileType.Xml)
                    new XmlFoldingStrategy().UpdateFoldings(tab.FoldingManager, tab.Editor.Document);
                else if (tab.FileType == FileType.Json)
                    UpdateJsonFoldings(tab.FoldingManager, tab.Editor.Document);
                else if (tab.FileType == FileType.Yaml)
                    UpdateYamlFoldings(tab.FoldingManager, tab.Editor.Document);
                else
                    UpdateEdifactFoldings(tab.FoldingManager, tab.Editor.Document);
            }
            catch { /* ignore parse errors during folding update */ }
        }
        else if (tab.FoldingManager is not null)
        {
            FoldingManager.Uninstall(tab.FoldingManager);
            tab.FoldingManager = null;
        }
    }

    /// <summary>Brace-based folding for JSON ({…} and […]).</summary>
    private static void UpdateJsonFoldings(FoldingManager manager, ICSharpCode.AvalonEdit.Document.TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var stack = new Stack<(int offset, char open)>();
        var text = document.Text;
        bool inString = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\\' && inString) { i++; continue; }
            if (ch == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (ch == '{' || ch == '[')
                stack.Push((i, ch));
            else if ((ch == '}' || ch == ']') && stack.Count > 0)
            {
                var (startOff, _) = stack.Pop();
                if (i > startOff + 1)
                    foldings.Add(new NewFolding(startOff, i + 1));
            }
        }
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        manager.UpdateFoldings(foldings, -1);
    }

    private static void UpdateYamlFoldings(FoldingManager manager, ICSharpCode.AvalonEdit.Document.TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var lineCount = document.LineCount;
        // Stack of (startOffset, indentLevel) for open folds
        var stack = new Stack<(int offset, int indent)>();

        for (int ln = 1; ln <= lineCount; ln++)
        {
            var docLine = document.GetLineByNumber(ln);
            var lineText = document.GetText(docLine.Offset, docLine.Length);
            if (string.IsNullOrWhiteSpace(lineText)) continue;
            // Skip comment-only lines for indent tracking
            var trimmed = lineText.TrimStart();
            if (trimmed.StartsWith('#')) continue;

            int indent = lineText.Length - trimmed.Length;

            // Close all blocks that have >= indent level
            while (stack.Count > 0 && stack.Peek().indent >= indent)
            {
                var (startOff, _) = stack.Pop();
                // End fold at end of previous non-blank line
                int endOff = docLine.Offset > 0 ? docLine.Offset - 1 : docLine.Offset;
                if (endOff > startOff)
                    foldings.Add(new NewFolding(startOff, endOff));
            }

            // If line ends with : or :  (mapping start), or is a sequence parent, open a fold
            if (trimmed.EndsWith(':') || trimmed.EndsWith(": |") || trimmed.EndsWith(": >")
                || trimmed.EndsWith(": |-") || trimmed.EndsWith(": >-"))
                stack.Push((docLine.Offset, indent));
        }
        // Close remaining open folds
        while (stack.Count > 0)
        {
            var (startOff, _) = stack.Pop();
            int endOff = document.TextLength;
            if (endOff > startOff)
                foldings.Add(new NewFolding(startOff, endOff));
        }
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        manager.UpdateFoldings(foldings, -1);
    }

    private static void UpdateEdifactFoldings(FoldingManager manager, ICSharpCode.AvalonEdit.Document.TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var lineCount = document.LineCount;

        // Build (1-based lineNumber, tag) for every segment line
        var segments = new List<(int lineNumber, string tag)>();
        for (int ln = 1; ln <= lineCount; ln++)
        {
            var docLine = document.GetLineByNumber(ln);
            var lineText = document.GetText(docLine.Offset, docLine.Length).TrimStart();
            if (lineText.Length == 0) continue;
            int tagEnd = 0;
            while (tagEnd < lineText.Length && char.IsUpper(lineText[tagEnd])) tagEnd++;
            if (tagEnd is >= 2 and <= 3)
                segments.Add((ln, lineText[..tagEnd]));
        }

        // UNH...UNT message folding (one fold per message)
        int unhLine = -1;
        foreach (var (ln, tag) in segments)
        {
            if (tag == "UNH") { unhLine = ln; }
            else if (tag == "UNT" && unhLine >= 0)
            {
                AddLineFolding(foldings, document, unhLine, ln);
                unhLine = -1;
            }
        }

        // UNB...UNZ interchange folding
        int unbLine = -1;
        foreach (var (ln, tag) in segments)
        {
            if (tag == "UNB") { unbLine = ln; }
            else if (tag == "UNZ" && unbLine >= 0)
            {
                AddLineFolding(foldings, document, unbLine, ln);
                unbLine = -1;
            }
        }

        // Definition-based segment group folding (requires bundled definitions)
        var unhEntry = segments.Find(s => s.tag == "UNH");
        var untEntry = segments.Find(s => s.tag == "UNT");
        if (unhEntry.lineNumber > 0 && untEntry.lineNumber > unhEntry.lineNumber)
        {
            var unhDocLine = document.GetLineByNumber(unhEntry.lineNumber);
            var unhText = document.GetText(unhDocLine.Offset, unhDocLine.Length);
            var msgId = TryParseUnhMessageId(unhText);
            if (msgId is var (msgType, directory))
            {
                var def = EdifactDefinitionService.LookupMessage(directory, msgType);
                if (def is not null)
                {
                    // Body segments between UNH and UNT (exclusive)
                    var body = segments
                        .Where(s => s.lineNumber > unhEntry.lineNumber && s.lineNumber < untEntry.lineNumber)
                        .Select(s => new EdifactService.SegmentEntry(s.tag, "", s.lineNumber))
                        .ToList();

                    var groupFoldings = EdifactStructuralValidator.GetGroupFoldings(body, def);
                    foreach (var (groupName, startLine, endLine) in groupFoldings)
                    {
                        if (startLine >= endLine || startLine < 1 || endLine > document.LineCount) continue;
                        // Start at the beginning of the trigger line so that when collapsed the
                        // group name ("SG1") appears on its own row above the first segment.
                        var s = document.GetLineByNumber(startLine);
                        var e = document.GetLineByNumber(endLine);
                        foldings.Add(new NewFolding(s.Offset, e.EndOffset) { Name = groupName });
                    }
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        manager.UpdateFoldings(foldings, -1);
    }

    private static NewFolding? AddLineFolding(List<NewFolding> foldings,
        ICSharpCode.AvalonEdit.Document.TextDocument document, int startLine, int endLine)
    {
        if (startLine >= endLine || startLine < 1 || endLine > document.LineCount) return null;
        var s = document.GetLineByNumber(startLine);
        var e = document.GetLineByNumber(endLine);
        var nf = new NewFolding(s.EndOffset, e.EndOffset);
        foldings.Add(nf);
        return nf;
    }

    /// <summary>
    /// Parses the UNH segment text to extract (messageType, directory).
    /// UNH+ref+MSGTYPE:D:96A:UN' → ("IFTMCS", "D96A")
    /// </summary>
    private static (string msgType, string directory)? TryParseUnhMessageId(string unhLine)
    {
        // Strip trailing terminator and split on element separator '+'
        var trimmed = unhLine.TrimEnd().TrimEnd('\'');
        var parts = trimmed.Split('+');
        if (parts.Length < 3) return null;
        // parts[2] = "MSGTYPE:version:release:org"
        var composite = parts[2].Split(':');
        if (composite.Length < 3) return null;
        var msgType = composite[0];
        var release = composite[2]; // e.g. "96A"
        if (string.IsNullOrEmpty(msgType) || string.IsNullOrEmpty(release)) return null;
        return (msgType, $"D{release}");
    }

    private static IHighlightingDefinition? GetOrCreateXmlHighlighting(bool dark)
    {
        ref var cache = ref (dark ? ref _xmlHighlighting : ref _xmlHighlightingLight);
        if (cache is not null) return cache;

        var c = _colorSettings;
        string xshd = $"""
            <?xml version="1.0"?>
            <SyntaxDefinition name="{(dark ? "XmlVivid" : "XmlVividLight")}"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Comment"      foreground="{c.GetColor("XmlComment", dark)}" fontStyle="italic"/>
              <Color name="CData"        foreground="{c.GetColor("XmlCData", dark)}"/>
              <Color name="DocType"      foreground="{c.GetColor("XmlDocType", dark)}"/>
              <Color name="XmlDecl"      foreground="{c.GetColor("XmlDocType", dark)}"/>
              <Color name="TagName"      foreground="{c.GetColor("XmlTagName", dark)}" fontWeight="bold"/>
              <Color name="AttrName"     foreground="{c.GetColor("XmlAttrName", dark)}"/>
              <Color name="AttrValue"    foreground="{c.GetColor("XmlAttrValue", dark)}"/>
              <Color name="Entity"       foreground="{c.GetColor("XmlEntity", dark)}"/>
              <Color name="BracketPunct" foreground="{c.GetColor("XmlBracket", dark)}"/>
              <Color name="Text"        foreground="{c.GetColor("XmlText", dark)}"/>
              <RuleSet ignoreCase="false">
                <Span color="Comment" multiline="true"><Begin>&lt;!--</Begin><End>--&gt;</End></Span>
                <Span color="CData" multiline="true"><Begin>&lt;!\[CDATA\[</Begin><End>\]\]&gt;</End></Span>
                <Span color="DocType" multiline="true"><Begin>&lt;!DOCTYPE</Begin><End>&gt;</End></Span>
                <Span color="XmlDecl" multiline="true"><Begin>&lt;\?</Begin><End>\?&gt;</End></Span>
                <Span multiline="true">
                  <Begin color="BracketPunct">&lt;/?</Begin>
                  <End color="BracketPunct">/?></End>
                  <RuleSet>
                    <Rule color="AttrName">[a-zA-Z_][\w:.-]*(?=\s*=)</Rule>
                    <Span color="AttrValue"><Begin>"</Begin><End>"</End></Span>
                    <Span color="AttrValue"><Begin>'</Begin><End>'</End></Span>
                    <Rule color="TagName">[a-zA-Z_][\w:.-]*</Rule>
                  </RuleSet>
                </Span>
                <Rule color="Entity">&amp;[a-zA-Z]+;|&amp;#[0-9]+;|&amp;#x[0-9a-fA-F]+;</Rule>
                <Rule color="Text">[^&lt;&amp;]+</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        try { using var r = new XmlTextReader(new StringReader(xshd)); cache = HighlightingLoader.Load(r, HighlightingManager.Instance); }
        catch { cache = null; }
        return cache;
    }

    private static IHighlightingDefinition? GetOrCreateJsonHighlighting(bool dark)
    {
        ref var cache = ref (dark ? ref _jsonHighlighting : ref _jsonHighlightingLight);
        if (cache is not null) return cache;

        var c = _colorSettings;
        string xshd = $"""
            <?xml version="1.0"?>
            <SyntaxDefinition name="{(dark ? "Json" : "JsonLight")}"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Key"     foreground="{c.GetColor("JsonKey", dark)}"/>
              <Color name="String"  foreground="{c.GetColor("JsonString", dark)}"/>
              <Color name="Number"  foreground="{c.GetColor("JsonNumber", dark)}"/>
              <Color name="Keyword" foreground="{c.GetColor("JsonKeyword", dark)}"/>
              <RuleSet ignoreCase="false">
                <Rule color="Key">"(?:[^"\\]|\\.)*"(?=\s*:)</Rule>
                <Rule color="String">"(?:[^"\\]|\\.)*"</Rule>
                <Rule color="Number">-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?</Rule>
                <Rule color="Keyword">\b(true|false|null)\b</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        try { using var r = new XmlTextReader(new StringReader(xshd)); cache = HighlightingLoader.Load(r, HighlightingManager.Instance); }
        catch { cache = null; }
        return cache;
    }

    private static IHighlightingDefinition? GetOrCreateEdiHighlighting(bool dark)
    {
        ref var cache = ref (dark ? ref _ediHighlighting : ref _ediHighlightingLight);
        if (cache is not null) return cache;

        var c = _colorSettings;
        string xshd = $$"""
            <?xml version="1.0"?>
            <SyntaxDefinition name="{{(dark ? "Edifact" : "EdifactLight")}}"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="SegTag"  foreground="{{c.GetColor("EdiSegTag", dark)}}" fontWeight="bold"/>
              <Color name="SegTerm" foreground="{{c.GetColor("EdiSegTerm", dark)}}"/>
              <Color name="ElemSep" foreground="{{c.GetColor("EdiElemSep", dark)}}"/>
              <Color name="CompSep" foreground="{{c.GetColor("EdiCompSep", dark)}}"/>
              <Color name="Release" foreground="{{c.GetColor("EdiRelease", dark)}}"/>
              <Color name="Data"    foreground="{{c.GetColor("EdiData", dark)}}"/>
              <RuleSet ignoreCase="false">
                <Rule color="Release">\?.</Rule>
                <Rule color="SegTag">\b[A-Z]{2,3}(?=[+:'])</Rule>
                <Rule color="SegTerm">'</Rule>
                <Rule color="ElemSep">\+</Rule>
                <Rule color="CompSep">:</Rule>
                <Rule color="Data">[^+:?'\r\n]+</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        try { using var r = new XmlTextReader(new StringReader(xshd)); cache = HighlightingLoader.Load(r, HighlightingManager.Instance); }
        catch { cache = null; }
        return cache;
    }

    private static IHighlightingDefinition? GetOrCreateYamlHighlighting(bool dark)
    {
        ref var cache = ref (dark ? ref _yamlHighlighting : ref _yamlHighlightingLight);
        if (cache is not null) return cache;

        var c = _colorSettings;
        string xshd = $$"""
            <?xml version="1.0"?>
            <SyntaxDefinition name="{{(dark ? "Yaml" : "YamlLight")}}"
                xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Comment"   foreground="{{c.GetColor("YamlComment", dark)}}"/>
              <Color name="DocMarker" foreground="{{c.GetColor("YamlDocMarker", dark)}}" fontWeight="bold"/>
              <Color name="Anchor"    foreground="{{c.GetColor("YamlAnchor", dark)}}"/>
              <Color name="Tag"       foreground="{{c.GetColor("YamlTag", dark)}}"/>
              <Color name="Key"       foreground="{{c.GetColor("YamlKey", dark)}}"/>
              <Color name="Value"     foreground="{{c.GetColor("YamlValue", dark)}}"/>
              <RuleSet ignoreCase="false">
                <Span color="Comment" begin="\#" />
                <Rule color="DocMarker">^---\s*$|^\.\.\.\s*$</Rule>
                <Rule color="Anchor">[&amp;*][^\s,\]\}]+</Rule>
                <Rule color="Tag">![^\s]+</Rule>
                <Rule color="Key">^[\s]*[^#\s\-][^:]*(?=\s*:(\s|$))</Rule>
                <Rule color="Value">"[^"]*"|'[^']*'</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        try { using var r = new XmlTextReader(new StringReader(xshd)); cache = HighlightingLoader.Load(r, HighlightingManager.Instance); }
        catch { cache = null; }
        return cache;
    }

    // ──────────────────────────── zoom ────────────────────────────
    private void ChangeEditorZoom(EditorTab tab, int direction)
    {
        double newSize = Math.Clamp(tab.Editor.FontSize + direction, EditorFontSizeMin, EditorFontSizeMax);
        tab.Editor.FontSize = newSize;
        if (tab.InlineTreeView is not null)
            tab.InlineTreeView.FontSize = newSize;
        double scale = newSize / EditorFontSizeDefault;
        var transform = new System.Windows.Media.ScaleTransform(scale, scale);
        if (tab.SchemaTreeView is not null)
            tab.SchemaTreeView.LayoutTransform = transform;
        UpdateEditorZoomLabel(tab);
    }

    private void ResetEditorZoom(EditorTab tab)
    {
        tab.Editor.FontSize = EditorFontSizeDefault;
        if (tab.InlineTreeView is not null)
            tab.InlineTreeView.FontSize = EditorFontSizeDefault;
        if (tab.SchemaTreeView is not null)
            tab.SchemaTreeView.LayoutTransform = System.Windows.Media.Transform.Identity;
        UpdateEditorZoomLabel(tab);
    }

    private static void UpdateEditorZoomLabel(EditorTab tab)
    {
        if (tab.ZoomLabel is null) return;
        int pct = (int)Math.Round(tab.Editor.FontSize / EditorFontSizeDefault * 100);
        tab.ZoomLabel.Text = $"{pct}%";
    }

    private void ChangeRightPanelZoom(int direction)
    {
        _rightPanelFontSize = Math.Clamp(_rightPanelFontSize + direction, RightPanelFontSizeMin, RightPanelFontSizeMax);
        xpathInput.FontSize = _rightPanelFontSize;
        xpathResultsList.FontSize = _rightPanelFontSize;
        allPathsList.FontSize = _rightPanelFontSize;
        messagesList.FontSize = _rightPanelFontSize;
        UpdateRightPanelZoomLabel();
    }

    private void ResetRightPanelZoom()
    {
        _rightPanelFontSize = RightPanelFontSizeDefault;
        xpathInput.FontSize = _rightPanelFontSize;
        xpathResultsList.FontSize = _rightPanelFontSize;
        allPathsList.FontSize = _rightPanelFontSize;
        messagesList.FontSize = _rightPanelFontSize;
        UpdateRightPanelZoomLabel();
    }

    private void UpdateRightPanelZoomLabel()
    {
        int pct = (int)Math.Round(_rightPanelFontSize / RightPanelFontSizeDefault * 100);
        rightPanelZoomLabel.Text = $"{pct}%";
    }

    private void RightPanelZoomOut_Click(object sender, RoutedEventArgs e) => ChangeRightPanelZoom(-1);
    private void RightPanelZoomIn_Click(object sender, RoutedEventArgs e) => ChangeRightPanelZoom(+1);

    // ──────────────────────────── Go to Line ────────────────────────────

    private void GoToLine()
    {
        var tab = ActiveTab;
        if (tab is null) return;

        var dlg = new Window
        {
            Title = "Go to Line",
            Width = 300,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
        };
        var themePath = _isDarkMode ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        dlg.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        });
        dlg.SetResourceReference(Window.BackgroundProperty, "AppBackground");

        var panel = new StackPanel { Margin = new Thickness(12) };

        var maxLine = tab.Editor.Document.LineCount;
        var label = new TextBlock { Text = $"Line number (1–{maxLine}):", Margin = new Thickness(0, 0, 0, 6) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "LabelForeground");

        var input = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        input.SetResourceReference(TextBox.BackgroundProperty, "EditorBackground");
        input.SetResourceReference(TextBox.ForegroundProperty, "EditorForeground");

        var okBtn = new Button { Content = "Go", Width = 70, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        okBtn.SetResourceReference(Button.BackgroundProperty, "ActionButtonBackground");
        okBtn.Foreground = System.Windows.Media.Brushes.White;

        okBtn.Click += (s, e) =>
        {
            if (int.TryParse(input.Text.Trim(), out int line) && line >= 1 && line <= maxLine)
            {
                dlg.DialogResult = true;
                dlg.Close();
                tab.Editor.ScrollTo(line, 0);
                tab.Editor.TextArea.Caret.Line = line;
                tab.Editor.TextArea.Caret.Column = 1;
                tab.Editor.Focus();
                SetStatus($"Ln {line}");
            }
            else
            {
                input.Focus();
                input.SelectAll();
            }
        };

        panel.Children.Add(label);
        panel.Children.Add(input);
        panel.Children.Add(okBtn);
        dlg.Content = panel;
        input.Focus();
        dlg.ShowDialog();
    }

    // ──────────────────────────── Line / Column label ────────────────────────────

    private void UpdateLineColumnLabel(EditorTab tab)
    {
        var caret = tab.Editor.TextArea.Caret;
        lineColumnLabel.Text = $"Ln {caret.Line}, Col {caret.Column}";
    }

    // ──────────────────────────── Recent Files ────────────────────────────

    private const int MaxRecentFiles = 10;

    private void AddToRecentFiles(string filePath)
    {
        _recentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, filePath);
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        PopulateRecentFilesMenu();
    }

    private void PopulateRecentFilesMenu()
    {
        recentFilesMenu.Items.Clear();
        if (_recentFiles.Count == 0)
        {
            recentFilesMenu.IsEnabled = false;
            return;
        }
        recentFilesMenu.IsEnabled = true;
        foreach (var path in _recentFiles)
        {
            var item = new MenuItem { Header = path.Replace("_", "__") };
            var capturedPath = path;
            item.Click += (s, e) =>
            {
                if (File.Exists(capturedPath))
                    OpenFileFromPath(capturedPath);
                else
                {
                    SetStatus($"File not found: {capturedPath}");
                    _recentFiles.Remove(capturedPath);
                    PopulateRecentFilesMenu();
                }
            };
            recentFilesMenu.Items.Add(item);
        }
    }

    // ──────────────────────────── Query History ────────────────────────────

    private void XpathInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // When autocomplete popup is open, redirect Up/Down/Enter/Escape to it
        if (autocompletePopup.IsOpen)
        {
            if (e.Key == Key.Down)
            {
                if (autocompleteList.SelectedIndex < autocompleteList.Items.Count - 1)
                    autocompleteList.SelectedIndex++;
                autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                if (autocompleteList.SelectedIndex > 0)
                    autocompleteList.SelectedIndex--;
                autocompleteList.ScrollIntoView(autocompleteList.SelectedItem);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && autocompleteList.SelectedItem is string sel)
            {
                AcceptAutocomplete(sel);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Tab && autocompleteList.SelectedItem is string tabSel)
            {
                AcceptAutocomplete(tabSel);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                autocompletePopup.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Up && _queryHistory.Count > 0)
        {
            if (_queryHistoryIndex < _queryHistory.Count - 1)
                _queryHistoryIndex++;
            xpathInput.Text = _queryHistory[_queryHistoryIndex];
            xpathInput.CaretIndex = xpathInput.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _queryHistory.Count > 0)
        {
            if (_queryHistoryIndex > 0)
            {
                _queryHistoryIndex--;
                xpathInput.Text = _queryHistory[_queryHistoryIndex];
            }
            else
            {
                _queryHistoryIndex = -1;
                xpathInput.Text = string.Empty;
            }
            xpathInput.CaretIndex = xpathInput.Text.Length;
            e.Handled = true;
        }
    }

    // ──────────────────────────── XPath/JSONPath autocomplete ────────────────────────────

    private bool _suppressAutocomplete;

    private void XpathInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressAutocomplete) return;
        UpdateAutocomplete();
    }

    private void UpdateAutocomplete()
    {
        string text = xpathInput.Text.Trim();
        if (text.Length < 2 || _allPathsFullList.Count == 0)
        {
            autocompletePopup.IsOpen = false;
            return;
        }

        var matches = _allPathsFullList
            .Where(p => p.XPath.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                     || p.XPath.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.XPath)
            .Distinct()
            .Take(20)
            .ToList();

        if (matches.Count == 0 || (matches.Count == 1 && matches[0] == text))
        {
            autocompletePopup.IsOpen = false;
            return;
        }

        autocompleteList.ItemsSource = matches;
        autocompleteList.SelectedIndex = 0;
        autocompletePopup.IsOpen = true;
    }

    private void AcceptAutocomplete(string value)
    {
        _suppressAutocomplete = true;
        xpathInput.Text = value;
        xpathInput.CaretIndex = value.Length;
        autocompletePopup.IsOpen = false;
        _suppressAutocomplete = false;
        xpathInput.Focus();
    }

    private void AutocompleteList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void AutocompleteList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (autocompleteList.SelectedItem is string sel)
            AcceptAutocomplete(sel);
    }

    private void SetStatus(string message) => statusText.Text = message;

    private void ClearXPathResults()
    {
        xpathResultsList.ItemsSource = null;
        xpathResultsHeader.Text = "Results:";
    }

    // ──────────────────────────── Messages panel ────────────────────────────

    private void ShowMessages(List<MessageItem> messages)
    {
        messagesList.ItemsSource = messages;
        messagesHeader.Text = $"Messages  ({messages.Count})";
        rightTabs.SelectedItem = messagesTab;
    }

    private void ClearMessages()
    {
        messagesList.ItemsSource = null;
        messagesHeader.Text = "Messages";
    }

    private void MessagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Navigate to the line for a single-item selection only.
        if (messagesList.SelectedItems.Count != 1) return;
        if (messagesList.SelectedItem is not MessageItem item) return;
        if (item.LineNumber is not int line) return;
        var tab = ActiveTab;
        if (tab is null) return;
        NavigateToLine(tab, line, item.Message);
    }

    private void MessagesList_Copy(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (messagesList.SelectedItems.Count == 0) return;
        var sb = new System.Text.StringBuilder();
        foreach (MessageItem item in messagesList.SelectedItems)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(item.Display);
        }
        Clipboard.SetDataObject(sb.ToString());
    }

    private void ViewCodeList_Click(object sender, RoutedEventArgs e)
    {
        // Walk up from the Hyperlink to find the DataContext (MessageItem)
        if (sender is not System.Windows.Documents.Hyperlink hyperlink) return;
        var item = hyperlink.DataContext as MessageItem
                   ?? (hyperlink.Parent as System.Windows.FrameworkContentElement)?.DataContext as MessageItem;
        if (item is not { HasCodeList: true }) return;

        var codes = Services.EdifactDefinitionService.LookupCodeList(item.CodeListDirectory!, item.CodeListElementId!);
        if (codes.Count == 0) return;

        var win = new CodeListWindow(_isDarkMode, item.CodeListElementId!, item.CodeListElementName ?? item.CodeListElementId!, item.CodeListSegmentTag ?? "", codes);
        win.Owner = this;
        win.ShowDialog();
    }

    private List<MessageItem> ParseValidationErrors(string errorText)
    {
        var messages = new List<MessageItem>();
        foreach (var line in errorText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            int? lineNumber = null;
            string? clDirectory = null, clElementId = null, clSegTag = null, clElementName = null;

            // Extract [CodeList:directory:elementId:segTag:elementName] marker if present
            const string codeListMarker = " [CodeList:";
            int markerIdx = trimmed.IndexOf(codeListMarker, StringComparison.Ordinal);
            if (markerIdx >= 0)
            {
                int closeBracket = trimmed.IndexOf(']', markerIdx);
                if (closeBracket > markerIdx)
                {
                    var payload = trimmed[(markerIdx + codeListMarker.Length)..closeBracket];
                    var parts = payload.Split(':');
                    if (parts.Length >= 4)
                    {
                        clDirectory = parts[0];
                        clElementId = parts[1];
                        clSegTag = parts[2];
                        clElementName = string.Join(":", parts[3..]);  // name may contain ':'
                    }
                    // Strip the marker from displayed message
                    trimmed = trimmed[..markerIdx];
                }
            }

            // Try to extract line number from patterns like "Line 5:" or "Segment 3:"
            if (trimmed.StartsWith("Line ", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 5 && int.TryParse(trimmed[5..colonIdx], out int ln))
                {
                    lineNumber = ln;
                    // Strip the "Line N:" prefix from the message — Display will re-add it.
                    trimmed = trimmed[(colonIdx + 1)..].TrimStart();
                }
            }

            messages.Add(new MessageItem
            {
                Message = trimmed,
                LineNumber = lineNumber,
                CodeListDirectory = clDirectory,
                CodeListElementId = clElementId,
                CodeListSegmentTag = clSegTag,
                CodeListElementName = clElementName
            });
        }
        return messages;
    }

    private List<MessageItem> ParseExceptionError(string context, Exception ex)
    {
        var messages = new List<MessageItem>();
        int? lineNumber = null;

        // Try to extract line number from XmlException or similar
        if (ex is System.Xml.XmlException xmlEx && xmlEx.LineNumber > 0)
            lineNumber = xmlEx.LineNumber;
        else if (ex is Newtonsoft.Json.JsonReaderException jsonEx && jsonEx.LineNumber > 0)
            lineNumber = jsonEx.LineNumber;

        messages.Add(new MessageItem { Message = $"{context}: {ex.Message}", LineNumber = lineNumber });
        return messages;
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
