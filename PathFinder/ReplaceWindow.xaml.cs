using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;

namespace PathFinder;

public partial class ReplaceWindow : Window
{
    private readonly TextEditor _editor;
    private readonly Func<IReadOnlyList<TextEditor>>? _getAllEditors;

    public ReplaceWindow(bool isDark, TextEditor editor, Window owner, Func<IReadOnlyList<TextEditor>>? getAllEditors = null)
    {
        InitializeComponent();
        _editor = editor;
        _getAllEditors = getAllEditors;
        Owner = owner;

        ApplyTheme(isDark);

        // Pre-fill with selected text if any
        if (_editor.SelectionLength > 0 && !_editor.SelectedText.Contains('\n'))
            findBox.Text = _editor.SelectedText;

        findBox.Focus();
        findBox.SelectAll();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            { FindNext_Click(this, new RoutedEventArgs()); e.Handled = true; }
        };
    }

    public void ApplyTheme(bool isDark)
    {
        var themePath = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        });
    }

    private ISearchStrategy? CreateStrategy()
    {
        var pattern = findBox.Text;
        if (string.IsNullOrEmpty(pattern)) return null;

        try
        {
            var mode = useRegexCheck.IsChecked == true ? SearchMode.RegEx : SearchMode.Normal;
            return SearchStrategyFactory.Create(pattern,
                matchCaseCheck.IsChecked != true,
                wholeWordsCheck.IsChecked == true,
                mode);
        }
        catch
        {
            statusLabel.Text = "Invalid search pattern.";
            return null;
        }
    }

    private List<ISearchResult> FindAllMatches()
    {
        var strategy = CreateStrategy();
        if (strategy is null) return [];
        var doc = _editor.Document;
        return strategy.FindAll(doc, 0, doc.TextLength)
            .OrderBy(r => r.Offset)
            .ToList();
    }

    private void UpdateMatchCount()
    {
        var matches = FindAllMatches();
        statusLabel.Text = matches.Count == 0
            ? "No matches"
            : $"{matches.Count} match{(matches.Count == 1 ? "" : "es")}";
    }

    private void FindBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateMatchCount();

    private void Option_Changed(object sender, RoutedEventArgs e)
        => UpdateMatchCount();

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var matches = FindAllMatches();
        if (matches.Count == 0) { statusLabel.Text = "No matches"; return; }

        int caret = _editor.CaretOffset;
        var next = matches.FirstOrDefault(m => m.Offset >= caret)
            ?? matches[0]; // wrap around

        _editor.Select(next.Offset, next.Length);
        _editor.ScrollTo(_editor.Document.GetLineByOffset(next.Offset).LineNumber, 0);
        _editor.Focus();
        statusLabel.Text = $"Match at offset {next.Offset}";
    }

    private void FindPrev_Click(object sender, RoutedEventArgs e)
    {
        var matches = FindAllMatches();
        if (matches.Count == 0) { statusLabel.Text = "No matches"; return; }

        int caret = _editor.CaretOffset;
        var prev = matches.LastOrDefault(m => m.Offset + m.Length <= caret)
            ?? matches[^1]; // wrap around

        _editor.Select(prev.Offset, prev.Length);
        _editor.ScrollTo(_editor.Document.GetLineByOffset(prev.Offset).LineNumber, 0);
        _editor.Focus();
        statusLabel.Text = $"Match at offset {prev.Offset}";
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var strategy = CreateStrategy();
        if (strategy is null) return;

        // If the current selection is a match, replace it
        if (_editor.SelectionLength > 0)
        {
            var selResults = strategy.FindAll(_editor.Document,
                _editor.SelectionStart, _editor.SelectionLength).ToList();
            if (selResults.Count == 1 && selResults[0].Offset == _editor.SelectionStart
                && selResults[0].Length == _editor.SelectionLength)
            {
                _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength,
                    replaceBox.Text);
            }
        }

        // Move to next match
        FindNext_Click(sender, e);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (allDocsCheck.IsChecked == true)
        {
            ReplaceAllInAllDocuments();
            return;
        }

        var matches = FindAllMatches();
        if (matches.Count == 0) { statusLabel.Text = "No matches to replace."; return; }

        string replacement = replaceBox.Text;
        _editor.Document.BeginUpdate();
        try
        {
            // Replace backwards to preserve offsets
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                _editor.Document.Replace(m.Offset, m.Length, replacement);
            }
        }
        finally { _editor.Document.EndUpdate(); }

        statusLabel.Text = $"Replaced {matches.Count} occurrence{(matches.Count == 1 ? "" : "s")}.";
    }

    private void ReplaceAllInAllDocuments()
    {
        var strategy = CreateStrategy();
        if (strategy is null) return;
        var editors = _getAllEditors?.Invoke();
        if (editors is null || editors.Count == 0) { statusLabel.Text = "No open documents."; return; }

        string replacement = replaceBox.Text;
        int totalCount = 0;
        int docCount = 0;

        foreach (var editor in editors)
        {
            var doc = editor.Document;
            var matches = strategy.FindAll(doc, 0, doc.TextLength)
                .OrderByDescending(r => r.Offset)
                .ToList();
            if (matches.Count == 0) continue;

            doc.BeginUpdate();
            try
            {
                foreach (var m in matches)
                    doc.Replace(m.Offset, m.Length, replacement);
            }
            finally { doc.EndUpdate(); }

            totalCount += matches.Count;
            docCount++;
        }

        statusLabel.Text = totalCount == 0
            ? "No matches in any document."
            : $"Replaced {totalCount} occurrence{(totalCount == 1 ? "" : "s")} in {docCount} document{(docCount == 1 ? "" : "s")}.";
    }
}
