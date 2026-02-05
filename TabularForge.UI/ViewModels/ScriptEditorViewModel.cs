using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class ScriptEditorViewModel : ObservableObject
{
    private readonly ScriptingService _scriptingService;
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _executionCts;

    // === Editor State ===

    [ObservableProperty]
    private string _scriptCode = "// C# Script - Access 'Model' and 'Selected' objects\n// Use Output.WriteLine() for output\n\nOutput.WriteLine(\"Hello from TabularForge!\");\n";

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private ScriptExecutionState _executionState = ScriptExecutionState.Ready;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _executionTime = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    // === Model References (set by MainViewModel) ===

    [ObservableProperty]
    private TomNode? _modelRoot;

    [ObservableProperty]
    private TomNode? _selectedNode;

    // === Macros ===

    [ObservableProperty]
    private ObservableCollection<ScriptMacro> _macros = new();

    [ObservableProperty]
    private ScriptMacro? _selectedMacro;

    // === Snippets ===

    [ObservableProperty]
    private ObservableCollection<ScriptSnippet> _snippets = new();

    [ObservableProperty]
    private ScriptSnippet? _selectedSnippet;

    // === Completion ===

    [ObservableProperty]
    private ObservableCollection<ScriptCompletionItem> _completionItems = new();

    [ObservableProperty]
    private bool _isCompletionOpen;

    // === Recording ===

    [ObservableProperty]
    private bool _isRecording;

    // === Diagnostics ===

    [ObservableProperty]
    private ObservableCollection<ScriptDiagnostic> _diagnostics = new();

    // === Events ===

    public event EventHandler<string>? MessageLogged;

    public void LogMessage(string message) => MessageLogged?.Invoke(this, message);

    public ScriptEditorViewModel(ScriptingService scriptingService, ConnectionService connectionService)
    {
        _scriptingService = scriptingService;
        _connectionService = connectionService;

        // Load snippets
        foreach (var snippet in _scriptingService.Snippets)
            Snippets.Add(snippet);

        // Load macros
        RefreshMacros();
    }

    // === Execute Script ===

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsExecuting) return;
        if (string.IsNullOrWhiteSpace(ScriptCode))
        {
            OutputText = "No script to execute.";
            return;
        }

        IsExecuting = true;
        ExecutionState = ScriptExecutionState.Running;
        StatusText = "Executing...";
        OutputText = $"[{DateTime.Now:HH:mm:ss}] Executing script...\n";
        Diagnostics.Clear();

        try
        {
            _executionCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await _scriptingService.ExecuteScriptAsync(
                ScriptCode, ModelRoot, SelectedNode, _executionCts.Token);

            if (result.Success)
            {
                ExecutionState = ScriptExecutionState.Completed;
                OutputText += result.Output;
                OutputText += $"\n\n[{DateTime.Now:HH:mm:ss}] Completed in {result.Duration.TotalMilliseconds:F0}ms";
                StatusText = $"Completed ({result.Duration.TotalMilliseconds:F0}ms)";
                ExecutionTime = $"{result.Duration.TotalMilliseconds:F0}ms";
                LogMessage($"Script executed successfully ({result.Duration.TotalMilliseconds:F0}ms)");
            }
            else
            {
                ExecutionState = ScriptExecutionState.Failed;
                OutputText += $"\nERROR:\n{result.Error}";
                StatusText = "Failed";

                foreach (var diag in result.Diagnostics)
                    Diagnostics.Add(diag);

                LogMessage($"Script execution failed: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            ExecutionState = ScriptExecutionState.Failed;
            OutputText += "\n\nExecution cancelled (timeout or user).";
            StatusText = "Cancelled";
            LogMessage("Script execution cancelled.");
        }
        finally
        {
            IsExecuting = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _executionCts?.Cancel();
    }

    // === Clear Output ===

    [RelayCommand]
    private void ClearOutput()
    {
        OutputText = string.Empty;
        Diagnostics.Clear();
        StatusText = "Ready";
        ExecutionTime = string.Empty;
    }

    // === Snippet Insertion ===

    [RelayCommand]
    private void InsertSnippet(ScriptSnippet? snippet)
    {
        if (snippet == null) return;
        ScriptCode += $"\n\n{snippet.Code}";
        LogMessage($"Inserted snippet: {snippet.Name}");
    }

    [RelayCommand]
    private void NewScript()
    {
        ScriptCode = "// New C# Script\n// Access 'Model' and 'Selected' objects\n\n";
        OutputText = string.Empty;
        Diagnostics.Clear();
        StatusText = "Ready";
        ExecutionState = ScriptExecutionState.Ready;
    }

    // === Macro Commands ===

    [RelayCommand]
    private void SaveAsMacro()
    {
        var macro = new ScriptMacro
        {
            Name = $"Macro_{DateTime.Now:yyyyMMdd_HHmmss}",
            Description = "Saved from script editor",
            Code = ScriptCode,
            Category = "User"
        };

        _scriptingService.SaveMacro(macro);
        RefreshMacros();
        LogMessage($"Saved macro: {macro.Name}");
    }

    [RelayCommand]
    private void LoadMacro(ScriptMacro? macro)
    {
        if (macro == null) return;
        ScriptCode = macro.Code;
        LogMessage($"Loaded macro: {macro.Name}");
    }

    [RelayCommand]
    private void DeleteMacro(ScriptMacro? macro)
    {
        if (macro == null) return;
        _scriptingService.DeleteMacro(macro.Name);
        RefreshMacros();
        LogMessage($"Deleted macro: {macro.Name}");
    }

    [RelayCommand]
    private async Task RunMacro(ScriptMacro? macro)
    {
        if (macro == null) return;
        ScriptCode = macro.Code;
        await ExecuteScript();
    }

    private void RefreshMacros()
    {
        Macros.Clear();
        foreach (var macro in _scriptingService.Macros)
            Macros.Add(macro);
    }

    // === Macro Recording ===

    [RelayCommand]
    private void ToggleRecording()
    {
        if (IsRecording)
        {
            var code = _scriptingService.StopRecording();
            ScriptCode = code;
            IsRecording = false;
            StatusText = "Recording stopped";
            LogMessage("Macro recording stopped. Script generated.");
        }
        else
        {
            _scriptingService.StartRecording();
            IsRecording = true;
            StatusText = "Recording...";
            LogMessage("Macro recording started. Perform operations in the model.");
        }
    }

    // === Import/Export Macros ===

    [RelayCommand]
    private void ExportMacros()
    {
        var json = _scriptingService.ExportMacros();
        OutputText = $"Exported {Macros.Count} macros:\n\n{json}";
        LogMessage($"Exported {Macros.Count} macros");
    }

    [RelayCommand]
    private void ImportMacros()
    {
        // In a real implementation, this would open a file dialog
        LogMessage("Import macros: paste JSON in script editor and use Import function.");
    }

    // === Completion ===

    public async Task RequestCompletionsAsync(int position)
    {
        try
        {
            var items = await _scriptingService.GetCompletionsAsync(ScriptCode, position);
            CompletionItems.Clear();
            foreach (var item in items)
                CompletionItems.Add(item);
            IsCompletionOpen = items.Count > 0;
        }
        catch
        {
            IsCompletionOpen = false;
        }
    }

    [RelayCommand]
    private void DismissCompletion()
    {
        IsCompletionOpen = false;
        CompletionItems.Clear();
    }
}
