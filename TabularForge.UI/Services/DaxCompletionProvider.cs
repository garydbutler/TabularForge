using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using TabularForge.DAXParser.Lexer;
using TabularForge.DAXParser.Semantics;

namespace TabularForge.UI.Services;

public sealed class DaxCompletionProvider
{
    private readonly ModelInfo? _modelInfo;

    public DaxCompletionProvider(ModelInfo? modelInfo = null)
    {
        _modelInfo = modelInfo;
    }

    public List<ICompletionData> GetCompletions(TextDocument document, int offset, out int startOffset)
    {
        startOffset = offset;
        var completions = new List<ICompletionData>();

        if (document == null || offset <= 0)
            return completions;

        var text = document.Text;
        var context = AnalyzeContext(text, offset);

        switch (context.Type)
        {
            case CompletionContextType.TableReference:
                startOffset = context.PrefixStart;
                AddTableCompletions(completions, context.Prefix);
                break;

            case CompletionContextType.ColumnReference:
                startOffset = context.PrefixStart;
                AddColumnCompletions(completions, context.TableName, context.Prefix);
                break;

            case CompletionContextType.Function:
                startOffset = context.PrefixStart;
                AddFunctionCompletions(completions, context.Prefix);
                AddKeywordCompletions(completions, context.Prefix);
                AddTableCompletions(completions, context.Prefix);
                break;

            case CompletionContextType.General:
                startOffset = context.PrefixStart;
                AddFunctionCompletions(completions, context.Prefix);
                AddKeywordCompletions(completions, context.Prefix);
                AddTableCompletions(completions, context.Prefix);
                AddMeasureCompletions(completions, context.Prefix);
                break;
        }

        return completions;
    }

    private CompletionContext AnalyzeContext(string text, int offset)
    {
        if (offset <= 0 || offset > text.Length)
            return new CompletionContext(CompletionContextType.General, string.Empty, offset);

        // Check if we're inside a column reference [...]
        int bracketStart = -1;
        for (int i = offset - 1; i >= 0; i--)
        {
            if (text[i] == '[')
            {
                bracketStart = i;
                break;
            }
            if (text[i] == ']' || text[i] == '\n' || text[i] == '\r')
                break;
        }

        if (bracketStart >= 0)
        {
            var prefix = text[(bracketStart + 1)..offset];
            // Try to find table name before the bracket
            string? tableName = FindTableNameBefore(text, bracketStart);
            return new CompletionContext(CompletionContextType.ColumnReference, prefix, bracketStart + 1)
            {
                TableName = tableName
            };
        }

        // Check if we're inside a table reference '...'
        int quoteStart = -1;
        for (int i = offset - 1; i >= 0; i--)
        {
            if (text[i] == '\'')
            {
                quoteStart = i;
                break;
            }
            if (text[i] == '\n' || text[i] == '\r')
                break;
        }

        if (quoteStart >= 0)
        {
            var prefix = text[(quoteStart + 1)..offset];
            return new CompletionContext(CompletionContextType.TableReference, prefix, quoteStart + 1);
        }

        // General identifier context - find the start of the current word
        int wordStart = offset;
        for (int i = offset - 1; i >= 0; i--)
        {
            if (char.IsLetterOrDigit(text[i]) || text[i] == '_')
                wordStart = i;
            else
                break;
        }

        var wordPrefix = text[wordStart..offset];

        // Check if preceded by a dot (method-like)
        if (wordStart > 0 && text[wordStart - 1] == '.')
        {
            return new CompletionContext(CompletionContextType.Function, wordPrefix, wordStart);
        }

        if (wordPrefix.Length > 0)
        {
            return new CompletionContext(CompletionContextType.General, wordPrefix, wordStart);
        }

        return new CompletionContext(CompletionContextType.General, string.Empty, offset);
    }

    private static string? FindTableNameBefore(string text, int offset)
    {
        // Look for 'TableName' or TableName before offset
        int end = offset;
        // Skip whitespace
        while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;

        if (end <= 0) return null;

        if (text[end - 1] == '\'')
        {
            // Quoted table name
            int start = end - 2;
            while (start >= 0 && text[start] != '\'') start--;
            if (start >= 0)
                return text[(start + 1)..(end - 1)];
        }
        else if (char.IsLetterOrDigit(text[end - 1]) || text[end - 1] == '_')
        {
            // Unquoted identifier
            int start = end - 1;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;
            return text[start..end];
        }

        return null;
    }

    private void AddTableCompletions(List<ICompletionData> completions, string prefix)
    {
        if (_modelInfo == null) return;

        foreach (var table in _modelInfo.Tables)
        {
            if (string.IsNullOrEmpty(prefix) ||
                table.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new DaxCompletionData(
                    $"'{table.Name}'",
                    $"Table: {table.Name}\nColumns: {table.Columns.Count}, Measures: {table.Measures.Count}",
                    CompletionItemKind.Table));
            }
        }
    }

    private void AddColumnCompletions(List<ICompletionData> completions, string? tableName, string prefix)
    {
        if (_modelInfo == null) return;

        IEnumerable<TableInfo> tables;
        if (tableName != null)
        {
            var table = _modelInfo.FindTable(tableName);
            tables = table != null ? new[] { table } : Enumerable.Empty<TableInfo>();
        }
        else
        {
            tables = _modelInfo.Tables;
        }

        foreach (var table in tables)
        {
            foreach (var col in table.Columns)
            {
                if (string.IsNullOrEmpty(prefix) ||
                    col.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new DaxCompletionData(
                        col.Name,
                        $"Column: [{col.Name}]\nTable: {table.Name}\nType: {col.DataType}",
                        CompletionItemKind.Column));
                }
            }

            foreach (var meas in table.Measures)
            {
                if (string.IsNullOrEmpty(prefix) ||
                    meas.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new DaxCompletionData(
                        meas.Name,
                        $"Measure: [{meas.Name}]\nTable: {table.Name}",
                        CompletionItemKind.Measure));
                }
            }
        }
    }

    private static void AddFunctionCompletions(List<ICompletionData> completions, string prefix)
    {
        IEnumerable<DaxFunctionSignature> functions;
        if (string.IsNullOrEmpty(prefix))
        {
            functions = DaxFunctionCatalog.GetAllFunctions().Take(50);
        }
        else
        {
            functions = DaxFunctionCatalog.SearchFunctions(prefix);
        }

        foreach (var func in functions)
        {
            completions.Add(new DaxCompletionData(
                func.Name,
                $"{func.GetSignatureText()}\n\n{func.Description}",
                CompletionItemKind.Function));
        }
    }

    private static void AddKeywordCompletions(List<ICompletionData> completions, string prefix)
    {
        var keywords = new[]
        {
            "EVALUATE", "DEFINE", "MEASURE", "VAR", "RETURN",
            "ORDER BY", "ASC", "DESC", "IN", "NOT", "AND", "OR",
            "TRUE", "FALSE", "BLANK", "TABLE", "COLUMN"
        };

        foreach (var kw in keywords)
        {
            if (string.IsNullOrEmpty(prefix) ||
                kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new DaxCompletionData(kw, $"Keyword: {kw}", CompletionItemKind.Keyword));
            }
        }
    }

    private void AddMeasureCompletions(List<ICompletionData> completions, string prefix)
    {
        if (_modelInfo == null) return;

        foreach (var table in _modelInfo.Tables)
        {
            foreach (var measure in table.Measures)
            {
                if (string.IsNullOrEmpty(prefix) ||
                    measure.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new DaxCompletionData(
                        $"[{measure.Name}]",
                        $"Measure: {measure.Name}\nTable: {table.Name}",
                        CompletionItemKind.Measure));
                }
            }
        }
    }
}

public enum CompletionContextType
{
    General,
    Function,
    TableReference,
    ColumnReference
}

public sealed class CompletionContext
{
    public CompletionContextType Type { get; }
    public string Prefix { get; }
    public int PrefixStart { get; }
    public string? TableName { get; set; }

    public CompletionContext(CompletionContextType type, string prefix, int prefixStart)
    {
        Type = type;
        Prefix = prefix;
        PrefixStart = prefixStart;
    }
}

public enum CompletionItemKind
{
    Function,
    Table,
    Column,
    Measure,
    Keyword
}

public sealed class DaxCompletionData : ICompletionData
{
    private readonly CompletionItemKind _kind;

    public DaxCompletionData(string text, string description, CompletionItemKind kind)
    {
        Text = text;
        Description = description;
        _kind = kind;
    }

    public ImageSource? Image => null;
    public string Text { get; }

    public object Content
    {
        get
        {
            var prefix = _kind switch
            {
                CompletionItemKind.Function => "f(x)",
                CompletionItemKind.Table => "TBL",
                CompletionItemKind.Column => "COL",
                CompletionItemKind.Measure => "[ ]",
                CompletionItemKind.Keyword => "KEY",
                _ => ""
            };

            var color = _kind switch
            {
                CompletionItemKind.Function => Colors.MediumPurple,
                CompletionItemKind.Table => Colors.MediumAquamarine,
                CompletionItemKind.Column => Colors.CornflowerBlue,
                CompletionItemKind.Measure => Colors.Goldenrod,
                CompletionItemKind.Keyword => Colors.IndianRed,
                _ => Colors.Gray
            };

            var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = prefix,
                FontSize = 10,
                Width = 30,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 4, 0)
            });
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = Text,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            return panel;
        }
    }

    public object Description { get; }

    public double Priority => _kind switch
    {
        CompletionItemKind.Column => 1.0,
        CompletionItemKind.Measure => 1.0,
        CompletionItemKind.Function => 0.8,
        CompletionItemKind.Table => 0.7,
        CompletionItemKind.Keyword => 0.5,
        _ => 0.0
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
