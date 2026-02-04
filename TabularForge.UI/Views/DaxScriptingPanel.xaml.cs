using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using TabularForge.Core.Models;
using TabularForge.DAXParser.Formatter;
using TabularForge.DAXParser.Lexer;
using TabularForge.DAXParser.Parser;
using TabularForge.DAXParser.Semantics;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class DaxScriptingPanel : UserControl
{
    public DaxScriptingPanel()
    {
        InitializeComponent();
        SetupEditor();
        Loaded += OnLoaded;
    }

    private void SetupEditor()
    {
        LoadDaxHighlighting();

        ScriptEditor.Options.EnableHyperlinks = false;
        ScriptEditor.Options.ConvertTabsToSpaces = true;
        ScriptEditor.Options.IndentationSize = 4;
        ScriptEditor.Options.HighlightCurrentLine = true;
        ScriptEditor.Options.AllowScrollBelowDocument = true;

        ScriptEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
        ScriptEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)), 1);

        // Bracket matching
        ScriptEditor.TextArea.TextView.BackgroundRenderers.Add(
            new BracketHighlightRenderer(ScriptEditor.TextArea));
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
                ScriptEditor.SyntaxHighlighting = highlighting;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load DAX highlighting: {ex.Message}");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Generate initial script from the model
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);

        if (vm?.ModelRoot != null && string.IsNullOrEmpty(ScriptEditor.Text))
        {
            GenerateScript(vm.ModelRoot);
        }
    }

    public void GenerateScript(TomNode modelRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// DAX Script - TabularForge");
        sb.AppendLine("// Edit measures and calculated columns below, then click 'Apply to Model'");
        sb.AppendLine();

        GenerateScriptForNode(modelRoot, sb);
        ScriptEditor.Text = sb.ToString();
    }

    private void GenerateScriptForNode(TomNode node, StringBuilder sb)
    {
        foreach (var child in node.Children)
        {
            if (child.ObjectType == TomObjectType.Measure && !string.IsNullOrEmpty(child.Expression))
            {
                var tableName = FindTableName(child);
                sb.AppendLine($"MEASURE '{tableName}'[{child.Name}] =");
                sb.AppendLine($"    {child.Expression.Replace("\n", "\n    ")}");
                sb.AppendLine();
            }
            else if (child.ObjectType == TomObjectType.CalculatedColumn && !string.IsNullOrEmpty(child.Expression))
            {
                var tableName = FindTableName(child);
                sb.AppendLine($"COLUMN '{tableName}'[{child.Name}] =");
                sb.AppendLine($"    {child.Expression.Replace("\n", "\n    ")}");
                sb.AppendLine();
            }

            GenerateScriptForNode(child, sb);
        }
    }

    private static string FindTableName(TomNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current.ObjectType == TomObjectType.Table)
                return current.Name;
            current = current.Parent;
        }
        return "Unknown";
    }

    private void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm == null) return;

        var modelInfo = vm.BuildModelInfo();
        var analyzer = new DaxSemanticAnalyzer(modelInfo);
        var diagnostics = analyzer.Analyze(ScriptEditor.Text, "DAX Script");

        vm.ErrorList.UpdateDiagnostics(diagnostics);
        vm.AddMessage($"DAX script check: {diagnostics.Count(d => d.Severity == DaxDiagnosticSeverity.Error)} errors, " +
                       $"{diagnostics.Count(d => d.Severity == DaxDiagnosticSeverity.Warning)} warnings");
    }

    private void FormatButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ScriptEditor.Text)) return;

        try
        {
            var formatter = new DaxFormatter();
            ScriptEditor.Text = formatter.Format(ScriptEditor.Text);
        }
        catch (Exception ex)
        {
            var vm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            vm?.AddMessage($"Format error: {ex.Message}");
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm?.ModelRoot == null) return;

        try
        {
            var lexer = new DaxLexer(ScriptEditor.Text);
            var tokens = lexer.Tokenize();
            var parser = new DaxParser(tokens);
            var ast = parser.Parse();

            int applied = 0;
            foreach (var stmt in ast.Statements)
            {
                if (stmt is DaxMeasureDefNode measureDef)
                {
                    var node = FindMeasureNode(vm.ModelRoot, measureDef.TableName, measureDef.MeasureName);
                    if (node != null && measureDef.Expression != null)
                    {
                        // Extract expression text from the script
                        var exprText = ExtractExpressionText(ScriptEditor.Text, measureDef);
                        if (!string.IsNullOrEmpty(exprText))
                        {
                            node.Expression = exprText;
                            applied++;
                        }
                    }
                }
            }

            vm.AddMessage($"Applied {applied} definition(s) from DAX script to model.");
        }
        catch (Exception ex)
        {
            vm.AddMessage($"Script apply error: {ex.Message}");
        }
    }

    private static string ExtractExpressionText(string fullText, DaxAstNode node)
    {
        if (node is DaxMeasureDefNode m && m.Expression != null)
        {
            int start = m.Expression.StartOffset;
            int end = m.Expression.EndOffset;
            if (start >= 0 && end > start && end <= fullText.Length)
                return fullText[start..end].Trim();
        }
        return string.Empty;
    }

    private static TomNode? FindMeasureNode(TomNode root, string tableName, string measureName)
    {
        foreach (var child in root.Children)
        {
            if (child.ObjectType == TomObjectType.Measure &&
                child.Name.Equals(measureName, StringComparison.OrdinalIgnoreCase))
            {
                var parentTable = child.Parent;
                while (parentTable != null && parentTable.ObjectType != TomObjectType.Table)
                    parentTable = parentTable.Parent;
                if (parentTable != null && parentTable.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            var result = FindMeasureNode(child, tableName, measureName);
            if (result != null) return result;
        }
        return null;
    }
}
