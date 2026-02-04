using TabularForge.Core.Models;

namespace TabularForge.Core.Commands;

/// <summary>
/// Command to rename a TOM object.
/// </summary>
public class RenameCommand : IModelCommand
{
    private readonly TomNode _node;
    private readonly string _oldName;
    private readonly string _newName;

    public string Description => $"Rename '{_oldName}' to '{_newName}'";

    public RenameCommand(TomNode node, string newName)
    {
        _node = node;
        _oldName = node.Name;
        _newName = newName;
    }

    public void Execute()
    {
        _node.Name = _newName;
        if (_node.JsonObject != null)
            _node.JsonObject["name"] = _newName;
    }

    public void Undo()
    {
        _node.Name = _oldName;
        if (_node.JsonObject != null)
            _node.JsonObject["name"] = _oldName;
    }
}
