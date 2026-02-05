using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

// === Script Execution ===

public enum ScriptExecutionState
{
    Ready,
    Running,
    Completed,
    Failed
}

public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public object? ReturnValue { get; set; }
    public TimeSpan Duration { get; set; }
    public List<ScriptDiagnostic> Diagnostics { get; set; } = new();
}

public class ScriptDiagnostic
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ScriptDiagnosticSeverity Severity { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

public enum ScriptDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

// === Macro System ===

public partial class ScriptMacro : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _shortcutKey = string.Empty;

    [ObservableProperty]
    private bool _showInToolbar;

    [ObservableProperty]
    private string _contextObjectType = string.Empty; // e.g. "Measure", "Table", empty = any

    [ObservableProperty]
    private string _category = "General";

    public string FilePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.Now;
}

// === Script Snippet ===

public class ScriptSnippet
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Shortcut { get; set; } = string.Empty;
}

// === Completion Item ===

public class ScriptCompletionItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string InsertText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScriptCompletionKind Kind { get; set; }
}

public enum ScriptCompletionKind
{
    Keyword,
    Method,
    Property,
    Field,
    Class,
    Namespace,
    Variable,
    Snippet
}

// === Script Globals (available to scripts at runtime) ===

public class ScriptGlobals
{
    public TomNode? Model { get; set; }
    public TomNode? Selected { get; set; }
    public ScriptOutputWriter Output { get; set; } = new();
}

public class ScriptOutputWriter
{
    private readonly List<string> _lines = new();

    public void WriteLine(string text)
    {
        _lines.Add(text);
        LineWritten?.Invoke(this, text);
    }

    public void Write(string text)
    {
        _lines.Add(text);
        LineWritten?.Invoke(this, text);
    }

    public void Info(string text) => WriteLine($"[INFO] {text}");
    public void Warning(string text) => WriteLine($"[WARN] {text}");
    public void Error(string text) => WriteLine($"[ERROR] {text}");

    public string GetFullOutput() => string.Join(Environment.NewLine, _lines);
    public void Clear() => _lines.Clear();

    public event EventHandler<string>? LineWritten;
}

// === Macro Recording ===

public class RecordedOperation
{
    public string ObjectPath { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty; // PropertyChange, Rename, Delete, etc.
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string ToCSharpCode()
    {
        return OperationType switch
        {
            "PropertyChange" => $"// Change {PropertyName} on {ObjectPath}\n" +
                               $"// Old: {OldValue} -> New: {NewValue}",
            "Rename" => $"// Rename: {OldValue} -> {NewValue}\n" +
                        $"// Path: {ObjectPath}",
            _ => $"// {OperationType}: {ObjectPath}"
        };
    }
}
