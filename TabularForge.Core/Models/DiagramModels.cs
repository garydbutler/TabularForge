using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

// === Enums ===

public enum Cardinality
{
    OneToMany,
    ManyToOne,
    OneToOne,
    ManyToMany
}

public enum CrossFilterDirection
{
    Single,
    Both
}

public enum DiagramLayoutMode
{
    Tree,
    ForceDirected,
    Hierarchical
}

// === Diagram Layout (Save/Load) ===

public class DiagramLayout
{
    public string Name { get; set; } = "Default";
    public List<TableCardPosition> TablePositions { get; set; } = new();
    public double ZoomLevel { get; set; } = 1.0;
    public double PanX { get; set; }
    public double PanY { get; set; }
    public bool SnapToGrid { get; set; } = true;
    public double GridSize { get; set; } = 20.0;
}

public class TableCardPosition
{
    public string TableName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string GroupColor { get; set; } = "#2D2D30";
}

// === Table Card ===

public partial class DiagramTableCard : ObservableObject
{
    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 200;

    [ObservableProperty]
    private double _height = 250;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    private string _groupColor = "#2D2D30";

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<DiagramColumn> Columns { get; } = new();
    public ObservableCollection<DiagramMeasure> Measures { get; } = new();

    public int RowCount { get; set; }
}

public class DiagramColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string TableName { get; set; } = string.Empty;
}

public class DiagramMeasure
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

// === Relationship Line ===

public partial class DiagramRelationship : ObservableObject
{
    [ObservableProperty]
    private string _fromTable = string.Empty;

    [ObservableProperty]
    private string _fromColumn = string.Empty;

    [ObservableProperty]
    private string _toTable = string.Empty;

    [ObservableProperty]
    private string _toColumn = string.Empty;

    [ObservableProperty]
    private Cardinality _cardinality = Cardinality.OneToMany;

    [ObservableProperty]
    private CrossFilterDirection _crossFilterDirection = CrossFilterDirection.Single;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayCardinality => Cardinality switch
    {
        Cardinality.OneToMany => "1:*",
        Cardinality.ManyToOne => "*:1",
        Cardinality.OneToOne => "1:1",
        Cardinality.ManyToMany => "*:*",
        _ => "?"
    };

    public string DisplayName => $"{FromTable}[{FromColumn}] -> {ToTable}[{ToColumn}]";
}
