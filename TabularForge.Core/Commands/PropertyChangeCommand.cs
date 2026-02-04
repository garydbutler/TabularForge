using TabularForge.Core.Models;

namespace TabularForge.Core.Commands;

/// <summary>
/// Command that changes a property value on a TomNode, supporting undo/redo.
/// </summary>
public class PropertyChangeCommand : IModelCommand
{
    private readonly TomNode _node;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public string Description => $"Change {_propertyName} on '{_node.Name}'";

    public PropertyChangeCommand(TomNode node, string propertyName, object? oldValue, object? newValue)
    {
        _node = node;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        ApplyValue(_newValue);
    }

    public void Undo()
    {
        ApplyValue(_oldValue);
    }

    private void ApplyValue(object? value)
    {
        switch (_propertyName)
        {
            case "Name":
                _node.Name = value?.ToString() ?? string.Empty;
                break;
            case "Description":
                _node.Description = value?.ToString() ?? string.Empty;
                break;
            case "Expression":
                _node.Expression = value?.ToString() ?? string.Empty;
                break;
            case "FormatString":
                _node.FormatString = value?.ToString() ?? string.Empty;
                break;
            case "IsHidden":
                _node.IsHidden = value is true;
                break;
            case "DisplayFolder":
                _node.DisplayFolder = value?.ToString() ?? string.Empty;
                break;
            case "DataType":
                _node.DataType = value?.ToString() ?? string.Empty;
                break;
        }

        // Also update the JSON object if present
        if (_node.JsonObject != null)
        {
            var jsonPropName = char.ToLowerInvariant(_propertyName[0]) + _propertyName[1..];
            if (value != null)
                _node.JsonObject[jsonPropName] = Newtonsoft.Json.Linq.JToken.FromObject(value);
            else
                _node.JsonObject.Remove(jsonPropName);
        }
    }
}
