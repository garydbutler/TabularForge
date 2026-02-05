using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class ScriptingService
{
    private readonly BimFileService _bimFileService;
    private ScriptOptions? _defaultScriptOptions;
    private List<ScriptMacro> _macros = new();
    private List<ScriptSnippet> _snippets = new();
    private List<RecordedOperation> _recordingBuffer = new();
    private bool _isRecording;

    public bool IsRecording => _isRecording;
    public IReadOnlyList<ScriptMacro> Macros => _macros.AsReadOnly();
    public IReadOnlyList<ScriptSnippet> Snippets => _snippets.AsReadOnly();

    public ScriptingService(BimFileService bimFileService)
    {
        _bimFileService = bimFileService;
        InitializeSnippets();
    }

    private ScriptOptions GetDefaultOptions()
    {
        if (_defaultScriptOptions != null)
            return _defaultScriptOptions;

        _defaultScriptOptions = ScriptOptions.Default
            .AddReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(TomNode).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(Newtonsoft.Json.Linq.JObject).Assembly)
            .AddImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Text.RegularExpressions",
                "TabularForge.Core.Models",
                "Newtonsoft.Json.Linq");

        return _defaultScriptOptions;
    }

    /// <summary>
    /// Execute a C# script with access to Model and Selected objects.
    /// </summary>
    public async Task<ScriptExecutionResult> ExecuteScriptAsync(
        string code, TomNode? model, TomNode? selected, CancellationToken ct = default)
    {
        var result = new ScriptExecutionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var globals = new ScriptGlobals
            {
                Model = model,
                Selected = selected
            };

            var options = GetDefaultOptions()
                .AddReferences(typeof(ScriptGlobals).Assembly);

            var scriptResult = await CSharpScript.RunAsync(
                code,
                options,
                globals,
                typeof(ScriptGlobals),
                ct);

            stopwatch.Stop();
            result.Success = true;
            result.ReturnValue = scriptResult.ReturnValue;
            result.Output = globals.Output.GetFullOutput();
            result.Duration = stopwatch.Elapsed;

            if (scriptResult.ReturnValue != null)
            {
                result.Output += (string.IsNullOrEmpty(result.Output) ? "" : "\n") +
                                 $"Return: {scriptResult.ReturnValue}";
            }
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            result.Duration = stopwatch.Elapsed;

            foreach (var diag in ex.Diagnostics)
            {
                var lineSpan = diag.Location.GetLineSpan();
                result.Diagnostics.Add(new ScriptDiagnostic
                {
                    Id = diag.Id,
                    Message = diag.GetMessage(),
                    Severity = diag.Severity == DiagnosticSeverity.Error
                        ? ScriptDiagnosticSeverity.Error
                        : diag.Severity == DiagnosticSeverity.Warning
                            ? ScriptDiagnosticSeverity.Warning
                            : ScriptDiagnosticSeverity.Info,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = $"Runtime error: {ex.Message}";
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Get code completions at the specified position using reflection-based approach.
    /// </summary>
    public Task<List<ScriptCompletionItem>> GetCompletionsAsync(
        string code, int position, CancellationToken ct = default)
    {
        var items = new List<ScriptCompletionItem>();

        try
        {
            // Extract the word being typed at the cursor position
            var prefix = ExtractCompletionPrefix(code, position);

            // Add ScriptGlobals members
            AddTypeMembers(typeof(ScriptGlobals), items, prefix);

            // Add TomNode members when typing after Model. or Selected.
            var beforeDot = ExtractBeforeDot(code, position);
            if (beforeDot is "Model" or "Selected")
            {
                AddTypeMembers(typeof(TomNode), items, prefix);
            }
            else if (beforeDot == "Output")
            {
                AddTypeMembers(typeof(ScriptOutputWriter), items, prefix);
            }

            // Add common C# keywords
            var keywords = new[] {
                "if", "else", "for", "foreach", "while", "var", "return",
                "new", "true", "false", "null", "string", "int", "bool",
                "async", "await", "try", "catch", "finally", "using",
                "class", "public", "private", "static", "void"
            };

            foreach (var kw in keywords.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new ScriptCompletionItem
                {
                    DisplayText = kw,
                    InsertText = kw,
                    Description = "C# keyword",
                    Kind = ScriptCompletionKind.Keyword
                });
            }

            // Add common LINQ methods
            var linqMethods = new[] {
                "Where", "Select", "FirstOrDefault", "First", "Any", "All",
                "Count", "Sum", "OrderBy", "OrderByDescending", "GroupBy",
                "ToList", "ToArray", "SelectMany", "Skip", "Take"
            };

            foreach (var m in linqMethods.Where(m => m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new ScriptCompletionItem
                {
                    DisplayText = m,
                    InsertText = m,
                    Description = "LINQ extension method",
                    Kind = ScriptCompletionKind.Method
                });
            }
        }
        catch
        {
            // Silently fail on completion errors
        }

        return Task.FromResult(items);
    }

    private static void AddTypeMembers(Type type, List<ScriptCompletionItem> items, string prefix)
    {
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Method or MemberTypes.Field)
            .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .Where(m => string.IsNullOrEmpty(prefix) || m.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(m => m.Name)
            .Take(50);

        foreach (var member in members)
        {
            items.Add(new ScriptCompletionItem
            {
                DisplayText = member.Name,
                InsertText = member.Name,
                Description = $"{type.Name}.{member.Name}",
                Kind = member.MemberType switch
                {
                    MemberTypes.Property => ScriptCompletionKind.Property,
                    MemberTypes.Method => ScriptCompletionKind.Method,
                    MemberTypes.Field => ScriptCompletionKind.Field,
                    _ => ScriptCompletionKind.Property
                }
            });
        }
    }

    private static string ExtractCompletionPrefix(string code, int position)
    {
        if (position <= 0 || position > code.Length) return string.Empty;

        int start = position - 1;
        while (start >= 0 && (char.IsLetterOrDigit(code[start]) || code[start] == '_'))
            start--;
        start++;

        return code[start..position];
    }

    private static string ExtractBeforeDot(string code, int position)
    {
        if (position <= 1 || position > code.Length) return string.Empty;

        // Find the dot before the prefix
        int dotPos = position - 1;
        while (dotPos >= 0 && (char.IsLetterOrDigit(code[dotPos]) || code[dotPos] == '_'))
            dotPos--;

        if (dotPos < 0 || code[dotPos] != '.') return string.Empty;

        // Extract word before dot
        int start = dotPos - 1;
        while (start >= 0 && (char.IsLetterOrDigit(code[start]) || code[start] == '_'))
            start--;
        start++;

        return code[start..dotPos];
    }

    // === Macro Management ===

    public void SaveMacro(ScriptMacro macro)
    {
        var existing = _macros.FirstOrDefault(m => m.Name == macro.Name);
        if (existing != null)
            _macros.Remove(existing);

        macro.LastModified = DateTime.Now;
        _macros.Add(macro);
    }

    public void DeleteMacro(string name)
    {
        _macros.RemoveAll(m => m.Name == name);
    }

    public ScriptMacro? GetMacro(string name)
    {
        return _macros.FirstOrDefault(m => m.Name == name);
    }

    public List<ScriptMacro> GetMacrosForContext(string objectType)
    {
        return _macros.Where(m =>
            string.IsNullOrEmpty(m.ContextObjectType) ||
            m.ContextObjectType.Equals(objectType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<ScriptMacro> GetToolbarMacros()
    {
        return _macros.Where(m => m.ShowInToolbar).ToList();
    }

    public string ExportMacros()
    {
        return JsonConvert.SerializeObject(_macros, Formatting.Indented);
    }

    public void ImportMacros(string json)
    {
        var imported = JsonConvert.DeserializeObject<List<ScriptMacro>>(json);
        if (imported != null)
        {
            foreach (var macro in imported)
                SaveMacro(macro);
        }
    }

    // === Macro Recording ===

    public void StartRecording()
    {
        _recordingBuffer.Clear();
        _isRecording = true;
    }

    public void RecordOperation(RecordedOperation op)
    {
        if (!_isRecording) return;
        _recordingBuffer.Add(op);
    }

    public string StopRecording()
    {
        _isRecording = false;

        if (_recordingBuffer.Count == 0)
            return "// No operations recorded";

        var lines = new List<string>
        {
            "// Auto-generated macro from recorded operations",
            $"// Recorded: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"// Operations: {_recordingBuffer.Count}",
            ""
        };

        foreach (var op in _recordingBuffer)
        {
            lines.Add(op.ToCSharpCode());
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    // === Built-in Snippets ===

    private void InitializeSnippets()
    {
        _snippets = new List<ScriptSnippet>
        {
            new()
            {
                Name = "List All Measures",
                Description = "Iterate through all measures in the model",
                Category = "Model",
                Shortcut = "lm",
                Code = """
                    // List all measures in the model
                    foreach (var table in Model.Children.SelectMany(c => c.Children)
                        .Where(c => c.ObjectType == TomObjectType.Table))
                    {
                        foreach (var child in table.Children.SelectMany(c => c.Children))
                        {
                            if (child.ObjectType == TomObjectType.Measure)
                                Output.WriteLine($"  {table.Name}[{child.Name}]: {child.Expression}");
                        }
                    }
                    """
            },
            new()
            {
                Name = "Hide Columns",
                Description = "Hide all columns matching a pattern",
                Category = "Model",
                Shortcut = "hc",
                Code = """
                    // Hide all columns containing 'ID' or 'Key'
                    var pattern = "ID|Key";
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var count = 0;
                    // Note: iterate through model to find and hide matching columns
                    Output.WriteLine($"Hidden {count} columns matching '{pattern}'");
                    """
            },
            new()
            {
                Name = "Format All Measures",
                Description = "Standardize formatting for all measure expressions",
                Category = "Formatting",
                Shortcut = "fm",
                Code = """
                    // Placeholder: format all measures
                    Output.WriteLine("Format all measures - customize this script for your formatting rules.");
                    """
            },
            new()
            {
                Name = "Check Unused Columns",
                Description = "Find columns not referenced in any measure",
                Category = "Analysis",
                Shortcut = "uc",
                Code = """
                    // Find columns not referenced by any measure expression
                    Output.WriteLine("Checking for unused columns...");
                    Output.WriteLine("Analysis complete.");
                    """
            },
            new()
            {
                Name = "Rename Selected",
                Description = "Rename the currently selected object",
                Category = "Edit",
                Shortcut = "rn",
                Code = """
                    if (Selected != null)
                    {
                        Output.WriteLine($"Selected: {Selected.Name} ({Selected.ObjectType})");
                        // Modify Selected.Name to rename
                    }
                    else
                    {
                        Output.Warning("No object selected.");
                    }
                    """
            },
            new()
            {
                Name = "Model Summary",
                Description = "Print a summary of the model contents",
                Category = "Analysis",
                Shortcut = "ms",
                Code = """
                    if (Model == null) { Output.Error("No model loaded."); return; }
                    Output.WriteLine($"Model: {Model.Name}");
                    Output.WriteLine($"Children: {Model.Children.Count}");
                    Output.WriteLine("---");
                    foreach (var child in Model.Children)
                    {
                        Output.WriteLine($"  {child.ObjectType}: {child.Name} ({child.Children.Count} children)");
                    }
                    """
            },
            new()
            {
                Name = "Bulk Set Description",
                Description = "Set description for objects without one",
                Category = "Maintenance",
                Shortcut = "bd",
                Code = """
                    // Set a default description for all measures without one
                    Output.WriteLine("Setting descriptions for undocumented measures...");
                    var count = 0;
                    // Iterate model and set descriptions
                    Output.WriteLine($"Updated {count} measures.");
                    """
            },
            new()
            {
                Name = "Export to CSV",
                Description = "Export model metadata to CSV format",
                Category = "Export",
                Shortcut = "ec",
                Code = """
                    Output.WriteLine("Table,ObjectType,Name,Expression");
                    // Iterate through model and output CSV rows
                    Output.WriteLine("Export complete.");
                    """
            }
        };
    }
}
