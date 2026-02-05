using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using TabularForge.DAXParser.Formatter;
using TabularForge.DAXParser.Semantics;
using TabularForge.UI.Services;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class DaxEditorPanel : UserControl
{
    private bool _isUpdating;
    private CompletionWindow? _completionWindow;
    private DaxCompletionProvider? _completionProvider;
    private bool _findReplaceVisible;
    private bool _replaceVisible;

    public DaxEditorPanel()
    {
        InitializeComponent();
        SetupEditor();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void SetupEditor()
    {
        LoadDaxHighlighting();

        DaxEditor.Options.EnableHyperlinks = false;
        DaxEditor.Options.EnableEmailHyperlinks = false;
        DaxEditor.Options.ConvertTabsToSpaces = true;
        DaxEditor.Options.IndentationSize = 4;
        DaxEditor.Options.ShowBoxForControlCharacters = true;
        DaxEditor.Options.HighlightCurrentLine = true;
        DaxEditor.Options.AllowScrollBelowDocument = true;

        DaxEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
        DaxEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)), 1);

        // Bracket matching
        DaxEditor.TextArea.TextView.BackgroundRenderers.Add(
            new BracketHighlightRenderer(DaxEditor.TextArea));

        // Cursor position tracking
        DaxEditor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var vm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            if (vm != null)
            {
                vm.CursorPosition = $"Ln {DaxEditor.TextArea.Caret.Line}, Col {DaxEditor.TextArea.Caret.Column}";
            }
        };

        // IntelliSense triggers
        DaxEditor.TextArea.TextEntering += TextArea_TextEntering;
        DaxEditor.TextArea.TextEntered += TextArea_TextEntered;

        // Key bindings for Find/Replace and Format
        DaxEditor.TextArea.KeyDown += TextArea_KeyDown;

        // Initialize completion provider
        _completionProvider = new DaxCompletionProvider();
    }

    private void TextArea_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+F - Find
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFindBar(false);
            e.Handled = true;
        }
        // Ctrl+H - Replace
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFindBar(true);
            e.Handled = true;
        }
        // Ctrl+Shift+F - Format
        else if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            FormatDocument();
            e.Handled = true;
        }
        // Ctrl+Space - IntelliSense
        else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowCompletionWindow();
            e.Handled = true;
        }
        // Escape - close Find/Replace
        else if (e.Key == Key.Escape && _findReplaceVisible)
        {
            CloseFindReplace();
            e.Handled = true;
        }
    }

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (_completionWindow != null && e.Text.Length > 0)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == "[" || e.Text == "'" || e.Text == ".")
        {
            // Immediately show completions for column refs, table refs, and dot access
            ShowCompletionWindow();
        }
        else if (e.Text == "(")
        {
            // Close existing completion on opening paren (function call started)
            _completionWindow?.Close();
        }
        else if (e.Text.Length == 1 && char.IsLetter(e.Text[0]))
        {
            // Show auto-complete after typing first letter
            ShowCompletionWindow();
        }
    }

    private void ShowCompletionWindow()
    {
        if (_completionProvider == null) return;

        // Refresh model info from the ViewModel before showing completions
        EnsureModelInfo();

        var offset = DaxEditor.TextArea.Caret.Offset;
        var completions = _completionProvider.GetCompletions(DaxEditor.Document, offset, out int startOffset);

        if (completions.Count == 0)
        {
            _completionWindow?.Close();
            return;
        }

        _completionWindow = new CompletionWindow(DaxEditor.TextArea)
        {
            StartOffset = startOffset,
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = true
        };

        // Style the CompletionWindow for dark/light theme
        StyleCompletionWindow(_completionWindow);

        foreach (var item in completions)
            _completionWindow.CompletionList.CompletionData.Add(item);

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private void EnsureModelInfo()
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm?.IsModelLoaded == true && _completionProvider != null)
        {
            var modelInfo = vm.BuildModelInfo();
            if (modelInfo.Tables.Count > 0)
                _completionProvider = new DaxCompletionProvider(modelInfo);
        }
    }

    private static void StyleCompletionWindow(CompletionWindow window)
    {
        // Apply theme-aware colors to the CompletionWindow popup
        var popupBg = Application.Current.TryFindResource("PopupBackground") as SolidColorBrush;
        var popupBorder = Application.Current.TryFindResource("PopupBorderBrush") as SolidColorBrush;
        var primaryText = Application.Current.TryFindResource("PrimaryText") as SolidColorBrush;

        if (popupBg != null)
        {
            window.Background = popupBg;
            window.CompletionList.Background = popupBg;
            window.CompletionList.ListBox.Background = popupBg;
        }
        if (popupBorder != null)
        {
            window.BorderBrush = popupBorder;
            window.BorderThickness = new Thickness(1);
        }
        if (primaryText != null)
        {
            window.Foreground = primaryText;
            window.CompletionList.Foreground = primaryText;
            window.CompletionList.ListBox.Foreground = primaryText;
        }

        window.MinWidth = 280;
        window.MaxHeight = 300;
    }

    public void UpdateModelInfo(ModelInfo modelInfo)
    {
        _completionProvider = new DaxCompletionProvider(modelInfo);
    }

    // === Find/Replace ===

    private void ShowFindBar(bool showReplace)
    {
        _findReplaceVisible = true;
        _replaceVisible = showReplace;
        FindReplaceBar.Visibility = Visibility.Visible;
        ReplaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;

        // Pre-fill with selected text
        if (DaxEditor.SelectionLength > 0)
        {
            FindTextBox.Text = DaxEditor.SelectedText;
        }

        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void CloseFindReplace()
    {
        _findReplaceVisible = false;
        FindReplaceBar.Visibility = Visibility.Collapsed;
        DaxEditor.Focus();
        ClearHighlights();
    }

    private void CloseFindReplace_Click(object sender, RoutedEventArgs e)
    {
        CloseFindReplace();
    }

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindReplace();
            e.Handled = true;
        }
    }

    private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        HighlightAllMatches();
    }

    private void FindOptions_Changed(object sender, RoutedEventArgs e)
    {
        HighlightAllMatches();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindPrevious();

    private void FindNext()
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var startOffset = DaxEditor.SelectionStart + DaxEditor.SelectionLength;
        var match = FindMatch(searchText, startOffset, true);
        if (match != null)
        {
            DaxEditor.Select(match.Value.start, match.Value.length);
            DaxEditor.ScrollTo(DaxEditor.Document.GetLineByOffset(match.Value.start).LineNumber, 0);
        }
    }

    private void FindPrevious()
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var startOffset = DaxEditor.SelectionStart;
        var match = FindMatch(searchText, startOffset, false);
        if (match != null)
        {
            DaxEditor.Select(match.Value.start, match.Value.length);
            DaxEditor.ScrollTo(DaxEditor.Document.GetLineByOffset(match.Value.start).LineNumber, 0);
        }
    }

    private void ReplaceNext_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        // If current selection matches, replace it
        if (DaxEditor.SelectionLength > 0 && IsMatch(DaxEditor.SelectedText, searchText))
        {
            DaxEditor.Document.Replace(DaxEditor.SelectionStart, DaxEditor.SelectionLength, replaceText);
        }

        FindNext();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = DaxEditor.Text;
        string newText;
        int count;

        if (RegexCheck.IsChecked == true)
        {
            var options = CaseSensitiveCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(searchText, options);
            count = regex.Matches(text).Count;
            newText = regex.Replace(text, replaceText);
        }
        else
        {
            var comparison = CaseSensitiveCheck.IsChecked == true
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            count = CountOccurrences(text, searchText, comparison);
            newText = ReplaceAllOccurrences(text, searchText, replaceText, comparison);
        }

        if (count > 0)
        {
            _isUpdating = true;
            DaxEditor.Text = newText;
            _isUpdating = false;
            FindStatusText.Text = $"{count} replaced";
        }
    }

    private (int start, int length)? FindMatch(string searchText, int startOffset, bool forward)
    {
        var text = DaxEditor.Text;

        if (RegexCheck.IsChecked == true)
        {
            try
            {
                var options = CaseSensitiveCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(searchText, options);

                if (forward)
                {
                    var match = regex.Match(text, startOffset);
                    if (!match.Success)
                        match = regex.Match(text, 0); // Wrap around
                    if (match.Success)
                        return (match.Index, match.Length);
                }
                else
                {
                    var matches = regex.Matches(text);
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        if (matches[i].Index < startOffset)
                            return (matches[i].Index, matches[i].Length);
                    }
                    if (matches.Count > 0)
                        return (matches[^1].Index, matches[^1].Length); // Wrap
                }
            }
            catch
            {
                FindStatusText.Text = "Invalid regex";
                return null;
            }
        }
        else
        {
            var comparison = CaseSensitiveCheck.IsChecked == true
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            if (forward)
            {
                var idx = text.IndexOf(searchText, startOffset, comparison);
                if (idx < 0)
                    idx = text.IndexOf(searchText, 0, comparison); // Wrap
                if (idx >= 0)
                {
                    if (WholeWordCheck.IsChecked == true && !IsWholeWord(text, idx, searchText.Length))
                    {
                        // Try next occurrence
                        idx = text.IndexOf(searchText, idx + 1, comparison);
                    }
                    if (idx >= 0)
                        return (idx, searchText.Length);
                }
            }
            else
            {
                var idx = startOffset > 0
                    ? text.LastIndexOf(searchText, startOffset - 1, comparison)
                    : -1;
                if (idx < 0)
                    idx = text.LastIndexOf(searchText, text.Length - 1, comparison); // Wrap
                if (idx >= 0)
                    return (idx, searchText.Length);
            }
        }

        FindStatusText.Text = "No results";
        return null;
    }

    private bool IsMatch(string text, string pattern)
    {
        if (RegexCheck.IsChecked == true)
        {
            try
            {
                var options = CaseSensitiveCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.IsMatch(text, $"^{pattern}$", options);
            }
            catch { return false; }
        }

        var comparison = CaseSensitiveCheck.IsChecked == true
            ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Equals(pattern, comparison);
    }

    private void HighlightAllMatches()
    {
        ClearHighlights();
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) { FindStatusText.Text = ""; return; }

        var text = DaxEditor.Text;
        int count = 0;

        if (RegexCheck.IsChecked == true)
        {
            try
            {
                var options = CaseSensitiveCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
                count = new Regex(searchText, options).Matches(text).Count;
            }
            catch
            {
                FindStatusText.Text = "Invalid regex";
                return;
            }
        }
        else
        {
            var comparison = CaseSensitiveCheck.IsChecked == true
                ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            count = CountOccurrences(text, searchText, comparison);
        }

        FindStatusText.Text = $"{count} found";
    }

    private void ClearHighlights()
    {
        // AvalonEdit handles text selection highlighting natively
    }

    private static bool IsWholeWord(string text, int index, int length)
    {
        if (index > 0 && char.IsLetterOrDigit(text[index - 1])) return false;
        int end = index + length;
        if (end < text.Length && char.IsLetterOrDigit(text[end])) return false;
        return true;
    }

    private static int CountOccurrences(string text, string search, StringComparison comparison)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(search, idx, comparison)) >= 0)
        {
            count++;
            idx += search.Length;
        }
        return count;
    }

    private static string ReplaceAllOccurrences(string text, string search, string replace, StringComparison comparison)
    {
        var sb = new System.Text.StringBuilder();
        int lastIdx = 0;
        int idx;
        while ((idx = text.IndexOf(search, lastIdx, comparison)) >= 0)
        {
            sb.Append(text, lastIdx, idx - lastIdx);
            sb.Append(replace);
            lastIdx = idx + search.Length;
        }
        sb.Append(text, lastIdx, text.Length - lastIdx);
        return sb.ToString();
    }

    // === DAX Formatting ===

    private void FormatButton_Click(object sender, RoutedEventArgs e)
    {
        FormatDocument();
    }

    private void FormatDocument()
    {
        if (string.IsNullOrWhiteSpace(DaxEditor.Text)) return;

        try
        {
            var formatter = new DaxFormatter();
            var formatted = formatter.Format(DaxEditor.Text);
            _isUpdating = true;
            DaxEditor.Text = formatted;
            _isUpdating = false;

            var vm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            vm?.AddMessage("DAX expression formatted.");
        }
        catch (Exception ex)
        {
            var vm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            vm?.AddMessage($"Format error: {ex.Message}");
        }
    }

    // === Syntax Highlighting ===

    private void LoadDaxHighlighting()
    {
        // Use globally registered DAX highlighting from App.OnStartup
        var definition = HighlightingManager.Instance.GetDefinition("DAX");
        if (definition != null)
        {
            DaxEditor.SyntaxHighlighting = definition;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("DAX highlighting not found in HighlightingManager. Was RegisterDaxHighlighting() called in App.OnStartup?");
        }
    }

    // === Data Binding ===

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncEditorContent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= Vm_PropertyChanged;
        }
        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += Vm_PropertyChanged;
            SyncEditorContent();
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.DaxEditorContent) && !_isUpdating)
        {
            SyncEditorContent();
        }
    }

    private void SyncEditorContent()
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm == null) return;

        _isUpdating = true;
        if (DaxEditor.Text != vm.DaxEditorContent)
        {
            DaxEditor.Text = vm.DaxEditorContent;
        }
        _isUpdating = false;
    }

    private void DaxEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdating) return;

        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm == null) return;

        _isUpdating = true;
        vm.DaxEditorContent = DaxEditor.Text;

        if (vm.ActiveDocument != null)
        {
            vm.ActiveDocument.Content = DaxEditor.Text;
            vm.ActiveDocument.IsModified = true;
        }
        _isUpdating = false;
    }
}

/// <summary>
/// Bracket highlight renderer for the DAX editor.
/// Highlights matching parentheses, brackets, and braces.
/// </summary>
public class BracketHighlightRenderer : IBackgroundRenderer
{
    private readonly TextArea _textArea;
    private static readonly Brush MatchBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 204));
    private static readonly Brush MismatchBrush = new SolidColorBrush(Color.FromArgb(60, 244, 71, 71));

    public BracketHighlightRenderer(TextArea textArea)
    {
        _textArea = textArea;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_textArea.Document == null) return;

        var offset = _textArea.Caret.Offset;
        if (offset >= _textArea.Document.TextLength) return;

        var doc = _textArea.Document;
        var currentChar = offset < doc.TextLength ? doc.GetCharAt(offset) : '\0';
        var prevChar = offset > 0 ? doc.GetCharAt(offset - 1) : '\0';

        char openChar = '\0', closeChar = '\0';
        int searchOffset = -1;
        bool searchForward = true;

        if (IsOpenBracket(currentChar))
        {
            openChar = currentChar;
            closeChar = GetMatchingBracket(currentChar);
            searchOffset = offset;
            searchForward = true;
        }
        else if (IsCloseBracket(currentChar))
        {
            closeChar = currentChar;
            openChar = GetMatchingBracket(currentChar);
            searchOffset = offset;
            searchForward = false;
        }
        else if (IsOpenBracket(prevChar))
        {
            openChar = prevChar;
            closeChar = GetMatchingBracket(prevChar);
            searchOffset = offset - 1;
            searchForward = true;
        }
        else if (IsCloseBracket(prevChar))
        {
            closeChar = prevChar;
            openChar = GetMatchingBracket(prevChar);
            searchOffset = offset - 1;
            searchForward = false;
        }

        if (searchOffset < 0) return;

        int matchOffset = FindMatchingBracket(doc, searchOffset, openChar, closeChar, searchForward);

        if (matchOffset >= 0)
        {
            DrawBracketHighlight(textView, drawingContext, searchOffset, MatchBrush);
            DrawBracketHighlight(textView, drawingContext, matchOffset, MatchBrush);
        }
        else
        {
            // Unmatched bracket - highlight in red
            DrawBracketHighlight(textView, drawingContext, searchOffset, MismatchBrush);
        }
    }

    private static void DrawBracketHighlight(TextView textView, DrawingContext drawingContext, int offset, Brush brush)
    {
        var segment = new TextSegment { StartOffset = offset, Length = 1 };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            drawingContext.DrawRoundedRectangle(brush, null, rect, 2, 2);
        }
    }

    private static int FindMatchingBracket(TextDocument doc, int startOffset, char open, char close, bool forward)
    {
        int depth = 0;
        if (forward)
        {
            for (int i = startOffset; i < doc.TextLength; i++)
            {
                char c = doc.GetCharAt(i);
                if (c == open) depth++;
                else if (c == close) depth--;
                if (depth == 0) return i;
            }
        }
        else
        {
            for (int i = startOffset; i >= 0; i--)
            {
                char c = doc.GetCharAt(i);
                if (c == close) depth++;
                else if (c == open) depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static bool IsOpenBracket(char c) => c is '(' or '[' or '{';
    private static bool IsCloseBracket(char c) => c is ')' or ']' or '}';

    private static char GetMatchingBracket(char c) => c switch
    {
        '(' => ')',
        ')' => '(',
        '[' => ']',
        ']' => '[',
        '{' => '}',
        '}' => '{',
        _ => '\0'
    };
}
