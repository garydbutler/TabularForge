namespace TabularForge.Core.Commands;

/// <summary>
/// Interface for commands that modify the model. Supports undo/redo.
/// </summary>
public interface IModelCommand
{
    /// <summary>
    /// Human-readable description of the command for display in undo/redo menu.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undo the command, restoring previous state.
    /// </summary>
    void Undo();
}
