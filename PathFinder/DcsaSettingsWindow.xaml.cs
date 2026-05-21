using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PathFinder;

public partial class DcsaSettingsWindow : Window
{
    private readonly List<string> _urls;

    public List<string>? Result { get; private set; }

    public DcsaSettingsWindow(bool isDark, List<string> currentUrls)
    {
        InitializeComponent();
        ApplyTheme(isDark);
        _urls = currentUrls.ToList(); // deep copy
        RefreshList();
        if (_urls.Count > 0)
            urlListBox.SelectedIndex = 0;
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

    private void RefreshList()
    {
        var selectedIndex = urlListBox.SelectedIndex;
        urlListBox.Items.Clear();
        foreach (var url in _urls)
            urlListBox.Items.Add(url);
        if (selectedIndex >= 0 && selectedIndex < _urls.Count)
            urlListBox.SelectedIndex = selectedIndex;
    }

    private void UrlListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = urlListBox.SelectedItem as string;
        deleteButton.IsEnabled = selected is not null;
        if (selected is not null)
            urlTextBox.Text = selected;
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = urlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (_urls.Contains(url, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show("This URL is already in the list.", "Duplicate URL",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _urls.Add(url);
        RefreshList();
        urlListBox.SelectedIndex = _urls.Count - 1;
        urlTextBox.Clear();
    }

    private void UpdateUrl_Click(object sender, RoutedEventArgs e)
    {
        var index = urlListBox.SelectedIndex;
        if (index < 0) return;
        var url = urlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        _urls[index] = url;
        RefreshList();
        urlListBox.SelectedIndex = index;
    }

    private void DeleteUrl_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedUrl();
    }

    private void UrlListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            DeleteSelectedUrl();
    }

    private void DeleteSelectedUrl()
    {
        var index = urlListBox.SelectedIndex;
        if (index < 0) return;
        _urls.RemoveAt(index);
        RefreshList();
        if (_urls.Count > 0)
            urlListBox.SelectedIndex = Math.Min(index, _urls.Count - 1);
        urlTextBox.Clear();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _urls.Clear();
        _urls.AddRange(MainWindow.DefaultDcsaSchemaUrls);
        RefreshList();
        if (_urls.Count > 0)
            urlListBox.SelectedIndex = 0;
        urlTextBox.Clear();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = _urls.ToList();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
