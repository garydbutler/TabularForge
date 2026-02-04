using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Commands;

/// <summary>
/// Manages the undo/redo stacks for model commands.
/// </summary>
public partial class UndoRedoManager : ObservableObject
{
    private readonly Stack<IModelCommand> _undoStack = new();
    private readonly Stack<IModelCommand> _redoStack = new();
    private const int MaxUndoLevels = 500;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoDescription = string.Empty;

    [ObservableProperty]
    private string _redoDescription = string.Empty;

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Execute a command and push it onto the undo stack.
    /// </summary>
    public void Execute(IModelCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoLevels)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = Math.Min(items.Length - 1, MaxUndoLevels - 1); i >= 0; i--)
                _undoStack.Push(items[i]);
        }

        UpdateState();
    }

    /// <summary>
    /// Undo the last command.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        UpdateState();
    }

    /// <summary>
    /// Redo the last undone command.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        UpdateState();
    }

    /// <summary>
    /// Clear all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateState();
    }

    private void UpdateState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
        UndoDescription = CanUndo ? $"Undo {_undoStack.Peek().Description}" : "Undo";
        RedoDescription = CanRedo ? $"Redo {_redoStack.Peek().Description}" : "Redo";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
