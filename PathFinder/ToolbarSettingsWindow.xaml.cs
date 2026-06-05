using System.Windows;
using System.Windows.Controls;

namespace PathFinder;

public partial class ToolbarSettingsWindow : Window
{
    private readonly List<ToolbarItemConfig> _currentItems;
    private readonly Dictionary<string, string> _buttonDisplayNames;

    public List<ToolbarItemConfig>? Result { get; private set; }

    public ToolbarSettingsWindow(bool isDark, List<ToolbarItemConfig> currentLayout,
        IEnumerable<MainWindow.ToolbarButtonDefinition> registry)
    {
        InitializeComponent();
        ApplyTheme(isDark);

        _currentItems = currentLayout.Select(i => new ToolbarItemConfig(i.Id)).ToList();
        _buttonDisplayNames = registry.ToDictionary(d => d.Id, d => $"{d.Icon}  {d.Label}");

        RefreshLists();
        if (_currentItems.Count > 0)
            currentList.SelectedIndex = 0;
    }

    private void ApplyTheme(bool isDark)
    {
        var themePath = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        });
    }

    private string GetDisplayName(ToolbarItemConfig item)
    {
        if (item.IsSeparator) return "────── Separator ──────";
        return _buttonDisplayNames.TryGetValue(item.Id, out var name) ? name : item.Id;
    }

    private void RefreshLists()
    {
        var selectedCurrentIndex = currentList.SelectedIndex;

        currentList.Items.Clear();
        foreach (var item in _currentItems)
            currentList.Items.Add(GetDisplayName(item));

        if (selectedCurrentIndex >= 0 && selectedCurrentIndex < _currentItems.Count)
            currentList.SelectedIndex = selectedCurrentIndex;

        // Available = all buttons NOT in current (excluding separators — those can always be added)
        var usedIds = new HashSet<string>(_currentItems
            .Where(i => !i.IsSeparator)
            .Select(i => i.Id));

        availableList.Items.Clear();
        foreach (var kvp in _buttonDisplayNames)
        {
            if (!usedIds.Contains(kvp.Key))
                availableList.Items.Add(kvp.Value);
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var ci = currentList.SelectedIndex;
        moveUpButton.IsEnabled = ci > 0;
        moveDownButton.IsEnabled = ci >= 0 && ci < _currentItems.Count - 1;
        removeButton.IsEnabled = ci >= 0;
        addButton.IsEnabled = availableList.SelectedIndex >= 0;
    }

    private void CurrentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateButtonStates();

    private void AvailableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateButtonStates();

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var index = currentList.SelectedIndex;
        if (index <= 0) return;
        (_currentItems[index - 1], _currentItems[index]) = (_currentItems[index], _currentItems[index - 1]);
        RefreshLists();
        currentList.SelectedIndex = index - 1;
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = currentList.SelectedIndex;
        if (index < 0 || index >= _currentItems.Count - 1) return;
        (_currentItems[index], _currentItems[index + 1]) = (_currentItems[index + 1], _currentItems[index]);
        RefreshLists();
        currentList.SelectedIndex = index + 1;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var index = currentList.SelectedIndex;
        if (index < 0) return;
        _currentItems.RemoveAt(index);
        RefreshLists();
        if (_currentItems.Count > 0)
            currentList.SelectedIndex = Math.Min(index, _currentItems.Count - 1);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var availIndex = availableList.SelectedIndex;
        if (availIndex < 0) return;

        var displayText = availableList.Items[availIndex] as string;
        var entry = _buttonDisplayNames.FirstOrDefault(kvp => kvp.Value == displayText);
        if (entry.Key is null) return;

        // Insert after current selection, or at end
        var insertAt = currentList.SelectedIndex >= 0
            ? currentList.SelectedIndex + 1
            : _currentItems.Count;

        _currentItems.Insert(insertAt, new ToolbarItemConfig(entry.Key));
        RefreshLists();
        currentList.SelectedIndex = insertAt;
    }

    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        var insertAt = currentList.SelectedIndex >= 0
            ? currentList.SelectedIndex + 1
            : _currentItems.Count;

        _currentItems.Insert(insertAt, new ToolbarItemConfig(ToolbarItemConfig.SeparatorId));
        RefreshLists();
        currentList.SelectedIndex = insertAt;
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _currentItems.Clear();
        _currentItems.AddRange(MainWindow.DefaultToolbarLayout.Select(i => new ToolbarItemConfig(i.Id)));
        RefreshLists();
        if (_currentItems.Count > 0)
            currentList.SelectedIndex = 0;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = _currentItems.ToList();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
