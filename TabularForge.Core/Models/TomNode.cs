using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;

namespace TabularForge.Core.Models;

/// <summary>
/// Represents a node in the TOM Explorer tree. Each node corresponds to a
/// TOM object (table, column, measure, etc.) or a structural folder node.
/// </summary>
public partial class TomNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TomObjectType _objectType;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private string _formatString = string.Empty;

    [ObservableProperty]
    private string _dataType = string.Empty;

    [ObservableProperty]
    private string _displayFolder = string.Empty;

    /// <summary>
    /// The raw JSON object from the .bim file for this node.
    /// Used for reading/writing properties and round-tripping.
    /// </summary>
    public JObject? JsonObject { get; set; }

    /// <summary>
    /// Parent node in the tree.
    /// </summary>
    public TomNode? Parent { get; set; }

    /// <summary>
    /// Child nodes.
    /// </summary>
    public ObservableCollection<TomNode> Children { get; } = new();

    /// <summary>
    /// Properties available for the Properties panel.
    /// </summary>
    public List<TomProperty> Properties { get; set; } = new();

    /// <summary>
    /// Gets the icon key for this node based on its object type.
    /// </summary>
    public string IconKey => ObjectType switch
    {
        TomObjectType.Model => "ModelIcon",
        TomObjectType.DataSources => "FolderIcon",
        TomObjectType.DataSource => "DataSourceIcon",
        TomObjectType.Tables => "FolderIcon",
        TomObjectType.Table => "TableIcon",
        TomObjectType.Columns => "FolderIcon",
        TomObjectType.Column or TomObjectType.DataColumn => "ColumnIcon",
        TomObjectType.CalculatedColumn => "CalculatedColumnIcon",
        TomObjectType.CalculatedTableColumn => "CalculatedColumnIcon",
        TomObjectType.Measures => "FolderIcon",
        TomObjectType.Measure => "MeasureIcon",
        TomObjectType.Hierarchies => "FolderIcon",
        TomObjectType.Hierarchy => "HierarchyIcon",
        TomObjectType.HierarchyLevel => "LevelIcon",
        TomObjectType.Partitions => "FolderIcon",
        TomObjectType.Partition => "PartitionIcon",
        TomObjectType.Relationships => "FolderIcon",
        TomObjectType.Relationship => "RelationshipIcon",
        TomObjectType.Perspectives => "FolderIcon",
        TomObjectType.Perspective => "PerspectiveIcon",
        TomObjectType.Cultures => "FolderIcon",
        TomObjectType.Culture => "CultureIcon",
        TomObjectType.Roles => "FolderIcon",
        TomObjectType.Role => "RoleIcon",
        TomObjectType.CalculationGroups => "FolderIcon",
        TomObjectType.CalculationGroup => "CalculationGroupIcon",
        TomObjectType.CalculationItem => "CalculationItemIcon",
        TomObjectType.SharedExpressions => "FolderIcon",
        TomObjectType.SharedExpression => "ExpressionIcon",
        TomObjectType.Folder => "FolderIcon",
        _ => "FolderIcon"
    };

    public TomNode() { }

    public TomNode(string name, TomObjectType objectType, TomNode? parent = null)
    {
        Name = name;
        ObjectType = objectType;
        Parent = parent;
    }

    /// <summary>
    /// Builds the properties list from the JSON object for display in the Properties panel.
    /// </summary>
    public void BuildProperties()
    {
        Properties.Clear();

        Properties.Add(new TomProperty("Name", Name, "General"));
        Properties.Add(new TomProperty("Object Type", ObjectType.ToString(), "General", true));

        if (!string.IsNullOrEmpty(Description))
            Properties.Add(new TomProperty("Description", Description, "General"));

        if (!string.IsNullOrEmpty(Expression))
            Properties.Add(new TomProperty("Expression", Expression, "Expression"));

        if (!string.IsNullOrEmpty(DataType))
            Properties.Add(new TomProperty("Data Type", DataType, "Data"));

        if (!string.IsNullOrEmpty(FormatString))
            Properties.Add(new TomProperty("Format String", FormatString, "Formatting"));

        if (!string.IsNullOrEmpty(DisplayFolder))
            Properties.Add(new TomProperty("Display Folder", DisplayFolder, "Display"));

        Properties.Add(new TomProperty("Is Hidden", IsHidden, "Display"));

        if (JsonObject != null)
        {
            foreach (var prop in JsonObject.Properties())
            {
                if (prop.Name is "name" or "description" or "expression" or "dataType"
                    or "formatString" or "isHidden" or "displayFolder"
                    or "columns" or "measures" or "hierarchies" or "partitions" or "annotations")
                    continue;

                var val = prop.Value.Type switch
                {
                    JTokenType.String => (object?)prop.Value.ToString(),
                    JTokenType.Integer => prop.Value.ToObject<long>(),
                    JTokenType.Float => prop.Value.ToObject<double>(),
                    JTokenType.Boolean => prop.Value.ToObject<bool>(),
                    _ => prop.Value.ToString()
                };

                Properties.Add(new TomProperty(prop.Name, val, "Advanced", true));
            }
        }
    }

    /// <summary>
    /// Get the full path of this node in the tree.
    /// </summary>
    public string GetPath()
    {
        if (Parent == null) return Name;
        return $"{Parent.GetPath()}\\{Name}";
    }

    public override string ToString() => Name;
}
