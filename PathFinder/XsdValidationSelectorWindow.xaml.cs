using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PathFinder.Services;

namespace PathFinder;

/// <summary>
/// Selector dialog shown when the user clicks "Validate XML" or "Validate vs XSD".
/// Lists open tabs of the counterpart file type and provides a Browse button.
/// </summary>
public partial class XsdValidationSelectorWindow : Window
{
    /// <summary>Item view-model for the tab list.</summary>
    private sealed record TabEntry(string Title, string Content);

    private readonly bool _browsingForXsd;
    private readonly string _lastOpenDirectory;

    /// <summary>
    /// The content (text) of the selected counterpart file after the user clicks Validate.
    /// Null when the dialog is cancelled.
    /// </summary>
    public string? SelectedContent { get; private set; }

    /// <param name="isDark">Current theme.</param>
    /// <param name="activeIsXsd">
    ///     <c>true</c> when the active editor tab contains an XSD — the user must pick an XML.
    ///     <c>false</c> when the active tab contains XML — the user must pick an XSD.
    /// </param>
    /// <param name="openTabs">
    ///     Name/content pairs for all open tabs of the counterpart type that the user can pick.
    /// </param>
    /// <param name="lastOpenDirectory">Initial directory for the Browse dialog.</param>
    public XsdValidationSelectorWindow(
        bool isDark,
        bool activeIsXsd,
        IEnumerable<(string Title, string Content)> openTabs,
        string? lastOpenDirectory)
    {
        InitializeComponent();
        ApplyTheme(isDark);

        _browsingForXsd = !activeIsXsd;
        _lastOpenDirectory = lastOpenDirectory ?? string.Empty;

        headerText.Text = activeIsXsd
            ? "Select the XML document to validate against the active XSD schema."
            : "Select the XSD schema to validate the active XML document against.";

        browseButton.Content = _browsingForXsd ? "Browse for XSD file…" : "Browse for XML file…";

        foreach (var (title, content) in openTabs)
            tabListBox.Items.Add(new TabEntry(title, content));

        tabListBox.DisplayMemberPath = "Title";

        if (tabListBox.Items.Count > 0)
            tabListBox.SelectedIndex = 0;
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

    private void TabListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        validateButton.IsEnabled = tabListBox.SelectedItem is not null;
    }

    private void TabListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (tabListBox.SelectedItem is not null)
            AcceptSelectedTab();
    }

    private void Validate_Click(object sender, RoutedEventArgs e) => AcceptSelectedTab();

    private void AcceptSelectedTab()
    {
        if (tabListBox.SelectedItem is TabEntry entry)
        {
            SelectedContent = entry.Content;
            DialogResult = true;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = _browsingForXsd ? "Select XSD Schema File" : "Select XML File",
            Filter = _browsingForXsd
                ? "XSD Schema Files|*.xsd|All Files|*.*"
                : "XML Files|*.xml;*.xsd;*.xsl;*.xslt|All Files|*.*",
            InitialDirectory = _lastOpenDirectory,
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var (content, _) = EncodingService.ReadFileWithEncoding(dlg.FileName);
            SelectedContent = content;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not read file:\n{ex.Message}",
                "File Read Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
