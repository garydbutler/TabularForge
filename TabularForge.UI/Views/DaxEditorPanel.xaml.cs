using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class DaxEditorPanel : UserControl
{
    private bool _isUpdating;

    public DaxEditorPanel()
    {
        InitializeComponent();
        SetupEditor();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void SetupEditor()
    {
        // Load DAX syntax highlighting
        LoadDaxHighlighting();

        // Configure editor
        DaxEditor.Options.EnableHyperlinks = false;
        DaxEditor.Options.EnableEmailHyperlinks = false;
        DaxEditor.Options.ConvertTabsToSpaces = true;
        DaxEditor.Options.IndentationSize = 4;
        DaxEditor.Options.ShowBoxForControlCharacters = true;
        DaxEditor.Options.HighlightCurrentLine = true;
        DaxEditor.Options.AllowScrollBelowDocument = true;

        // Set current line highlight
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
    }

    private void LoadDaxHighlighting()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TabularForge.UI.SyntaxHighlighting.DAX.xshd";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                DaxEditor.SyntaxHighlighting = highlighting;
            }
            else
            {
                // Try loading from file as fallback
                var dir = Path.GetDirectoryName(assembly.Location) ?? ".";
                var xshdPath = Path.Combine(dir, "SyntaxHighlighting", "DAX.xshd");
                if (File.Exists(xshdPath))
                {
                    using var fileStream = File.OpenRead(xshdPath);
                    using var reader = new XmlTextReader(fileStream);
                    var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    DaxEditor.SyntaxHighlighting = highlighting;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load DAX highlighting: {ex.Message}");
        }
    }

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

        // Update active document if any
        if (vm.ActiveDocument != null)
        {
            vm.ActiveDocument.Content = DaxEditor.Text;
            vm.ActiveDocument.IsModified = true;
        }
        _isUpdating = false;
    }
}

/// <summary>
/// Simple bracket highlight renderer for the DAX editor.
/// Highlights matching parentheses, brackets, and braces.
/// </summary>
public class BracketHighlightRenderer : IBackgroundRenderer
{
    private readonly TextArea _textArea;
    private static readonly Brush MatchBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 204));

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

        // Check character before caret too
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
        if (matchOffset < 0) return;

        // Draw highlights
        DrawBracketHighlight(textView, drawingContext, searchOffset);
        DrawBracketHighlight(textView, drawingContext, matchOffset);
    }

    private void DrawBracketHighlight(TextView textView, DrawingContext drawingContext, int offset)
    {
        var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = offset, Length = 1 };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            drawingContext.DrawRoundedRectangle(MatchBrush, null, rect, 2, 2);
        }
    }

    private static int FindMatchingBracket(ICSharpCode.AvalonEdit.Document.TextDocument doc,
        int startOffset, char open, char close, bool forward)
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
