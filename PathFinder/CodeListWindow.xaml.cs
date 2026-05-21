using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PathFinder;

public partial class CodeListWindow : Window
{
    private readonly IReadOnlyList<CodeListEntry> _allEntries;

    internal CodeListWindow(bool isDark, string elementId, string elementName, string segmentTag, IReadOnlyDictionary<string, string> codes)
    {
        InitializeComponent();
        ApplyTheme(isDark);

        _allEntries = codes
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new CodeListEntry(kv.Key, kv.Value))
            .ToList();

        Title = $"Code List — {elementId} ({elementName})";
        headerText.Text = $"Element '{elementId}' ({elementName}) in segment '{segmentTag}' — {codes.Count} defined codes";

        // Build context menu in code-behind so Click handlers resolve correctly
        var copyMenuItem = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        copyMenuItem.Click += (s, e) => ApplicationCommands.Copy.Execute(null, codeList);
        var ctxMenu = new ContextMenu();
        ctxMenu.Items.Add(copyMenuItem);
        codeList.ContextMenu = ctxMenu;

        codeList.ItemsSource = _allEntries;
        UpdateCount(_allEntries.Count);
        codeList.SizeChanged += CodeList_SizeChanged;
        searchBox.Focus();
    }

    private void ApplyTheme(bool isDark)
    {
        var uri = new Uri(
            isDark ? "pack://application:,,,/PathFinder;component/Themes/DarkTheme.xaml"
                   : "pack://application:,,,/PathFinder;component/Themes/LightTheme.xaml",
            UriKind.Absolute);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = searchBox.Text.Trim();
        searchHint.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        searchClear.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Collapsed : Visibility.Visible;

        if (string.IsNullOrEmpty(query))
        {
            codeList.ItemsSource = _allEntries;
            UpdateCount(_allEntries.Count);
        }
        else
        {
            var filtered = _allEntries
                .Where(c => c.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || c.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            codeList.ItemsSource = filtered;
            UpdateCount(filtered.Count);
        }
    }

    private void SearchClear_Click(object sender, RoutedEventArgs e)
    {
        searchBox.Clear();
        searchBox.Focus();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void CodeList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var view = (GridView)codeList.View;
        var codeColumnWidth = view.Columns[0].ActualWidth;
        var remaining = codeList.ActualWidth - codeColumnWidth - 30; // 30 = scrollbar + padding
        if (remaining > 50)
            descriptionColumn.Width = remaining;
    }

    private void CodeList_Copy(object sender, ExecutedRoutedEventArgs e)
    {
        if (codeList.SelectedItems.Count == 0) return;
        var sb = new StringBuilder();
        foreach (CodeListEntry entry in codeList.SelectedItems)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(entry.Code);
        }
        Clipboard.SetDataObject(sb.ToString());
    }

    private void UpdateCount(int shown)
    {
        countLabel.Text = shown == _allEntries.Count
            ? $"{_allEntries.Count} codes"
            : $"{shown} / {_allEntries.Count} codes";
    }

    // ── View model ────────────────────────────────────────────────────────────

    internal sealed record CodeListEntry(string Code, string Description);
}
