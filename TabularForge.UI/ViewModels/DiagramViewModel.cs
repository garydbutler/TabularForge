using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class DiagramViewModel : ObservableObject
{
    private readonly DiagramService _diagramService;
    private readonly ConnectionService _connectionService;

    // === Collections ===

    [ObservableProperty]
    private ObservableCollection<DiagramTableCard> _tables = new();

    [ObservableProperty]
    private ObservableCollection<DiagramRelationship> _relationships = new();

    [ObservableProperty]
    private ObservableCollection<DiagramLayout> _savedLayouts = new();

    [ObservableProperty]
    private ObservableCollection<TreemapItem> _treemapItems = new();

    // === Selection State ===

    [ObservableProperty]
    private DiagramTableCard? _selectedTable;

    [ObservableProperty]
    private DiagramRelationship? _selectedRelationship;

    [ObservableProperty]
    private DiagramLayout? _activeLayout;

    // === View State ===

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private double _gridSize = 20.0;

    [ObservableProperty]
    private double _canvasWidth = 3000;

    [ObservableProperty]
    private double _canvasHeight = 2000;

    [ObservableProperty]
    private DiagramLayoutMode _layoutMode = DiagramLayoutMode.Tree;

    [ObservableProperty]
    private string _statusText = "No model loaded";

    [ObservableProperty]
    private bool _isModelLoaded;

    [ObservableProperty]
    private bool _showMinimap = true;

    // === Group Colors ===

    public string[] AvailableGroupColors { get; } = new[]
    {
        "#2D2D30", "#1E3A5F", "#3B1E3A", "#1E3B1E", "#4A3B1E",
        "#3B1E1E", "#1E3B3B", "#2E2E4A", "#4A2E2E", "#2E4A2E"
    };

    // === Events ===

    public event EventHandler<string>? MessageLogged;

    public void LogMessage(string message) => MessageLogged?.Invoke(this, message);

    public DiagramViewModel(DiagramService diagramService, ConnectionService connectionService)
    {
        _diagramService = diagramService;
        _connectionService = connectionService;

        // Create default layout
        var defaultLayout = new DiagramLayout { Name = "Default" };
        SavedLayouts.Add(defaultLayout);
        ActiveLayout = defaultLayout;
    }

    /// <summary>
    /// Load diagram from the model tree.
    /// </summary>
    public void LoadFromModel(TomNode? modelRoot)
    {
        Tables.Clear();
        Relationships.Clear();

        if (modelRoot == null)
        {
            StatusText = "No model loaded";
            IsModelLoaded = false;
            return;
        }

        var cards = _diagramService.ExtractTableCards(modelRoot);
        var rels = _diagramService.ExtractRelationships(modelRoot);
        _diagramService.MarkKeyColumns(cards, rels);

        // Try to restore positions from active layout
        if (ActiveLayout != null)
        {
            foreach (var card in cards)
            {
                var saved = ActiveLayout.TablePositions.FirstOrDefault(
                    p => p.TableName == card.TableName);
                if (saved != null)
                {
                    card.X = saved.X;
                    card.Y = saved.Y;
                    card.GroupColor = saved.GroupColor;
                }
            }
        }

        // If no positions saved, auto-layout
        bool needsLayout = cards.All(c => c.X == 0 && c.Y == 0);
        if (needsLayout)
        {
            ApplyLayoutInternal(cards, rels);
        }

        foreach (var card in cards)
            Tables.Add(card);
        foreach (var rel in rels)
            Relationships.Add(rel);

        IsModelLoaded = true;
        StatusText = $"{Tables.Count} tables, {Relationships.Count} relationships";
        MessageLogged?.Invoke(this, $"Diagram loaded: {Tables.Count} tables, {Relationships.Count} relationships");
    }

    // === Layout Commands ===

    [RelayCommand]
    private void ApplyLayout()
    {
        var tableList = Tables.ToList();
        var relList = Relationships.ToList();
        ApplyLayoutInternal(tableList, relList);

        // Update positions
        for (int i = 0; i < tableList.Count; i++)
        {
            Tables[i].X = tableList[i].X;
            Tables[i].Y = tableList[i].Y;
        }

        StatusText = $"Layout applied: {LayoutMode}";
        MessageLogged?.Invoke(this, $"Applied {LayoutMode} layout");
    }

    private void ApplyLayoutInternal(List<DiagramTableCard> tables, List<DiagramRelationship> rels)
    {
        switch (LayoutMode)
        {
            case DiagramLayoutMode.Tree:
                _diagramService.ApplyTreeLayout(tables, CanvasWidth);
                break;
            case DiagramLayoutMode.ForceDirected:
                _diagramService.ApplyForceDirectedLayout(tables, rels);
                break;
            case DiagramLayoutMode.Hierarchical:
                _diagramService.ApplyHierarchicalLayout(tables, rels);
                break;
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(3.0, ZoomLevel + 0.1);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(0.2, ZoomLevel - 0.1);
    }

    [RelayCommand]
    private void ZoomFit()
    {
        if (Tables.Count == 0) return;

        double minX = Tables.Min(t => t.X);
        double minY = Tables.Min(t => t.Y);
        double maxX = Tables.Max(t => t.X + t.Width);
        double maxY = Tables.Max(t => t.Y + t.Height);

        double contentW = maxX - minX + 80;
        double contentH = maxY - minY + 80;

        ZoomLevel = Math.Min(CanvasWidth / contentW, CanvasHeight / contentH);
        ZoomLevel = Math.Max(0.2, Math.Min(3.0, ZoomLevel));
        PanX = -minX + 40;
        PanY = -minY + 40;
    }

    [RelayCommand]
    private void ZoomReset()
    {
        ZoomLevel = 1.0;
        PanX = 0;
        PanY = 0;
    }

    // === Selection ===

    [RelayCommand]
    private void SelectTable(DiagramTableCard? table)
    {
        foreach (var t in Tables)
            t.IsSelected = false;
        foreach (var r in Relationships)
            r.IsSelected = false;

        if (table != null)
        {
            table.IsSelected = true;
            SelectedTable = table;
            SelectedRelationship = null;
        }
    }

    [RelayCommand]
    private void SelectRelationship(DiagramRelationship? rel)
    {
        foreach (var t in Tables)
            t.IsSelected = false;
        foreach (var r in Relationships)
            r.IsSelected = false;

        if (rel != null)
        {
            rel.IsSelected = true;
            SelectedRelationship = rel;
            SelectedTable = null;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var t in Tables)
            t.IsSelected = false;
        foreach (var r in Relationships)
            r.IsSelected = false;
        SelectedTable = null;
        SelectedRelationship = null;
    }

    // === Table Movement ===

    public void MoveTable(DiagramTableCard table, double newX, double newY)
    {
        if (SnapToGrid)
        {
            newX = DiagramService.SnapToGrid(newX, GridSize);
            newY = DiagramService.SnapToGrid(newY, GridSize);
        }

        table.X = Math.Max(0, newX);
        table.Y = Math.Max(0, newY);
    }

    public void MoveSelectedTables(double deltaX, double deltaY)
    {
        foreach (var t in Tables.Where(t => t.IsSelected))
        {
            MoveTable(t, t.X + deltaX, t.Y + deltaY);
        }
    }

    // === Group Color ===

    [RelayCommand]
    private void SetGroupColor(string? color)
    {
        if (color == null || SelectedTable == null) return;
        SelectedTable.GroupColor = color;
    }

    // === Layout Save/Load ===

    [RelayCommand]
    private void SaveCurrentLayout()
    {
        if (ActiveLayout == null) return;

        ActiveLayout.TablePositions.Clear();
        foreach (var t in Tables)
        {
            ActiveLayout.TablePositions.Add(new TableCardPosition
            {
                TableName = t.TableName,
                X = t.X,
                Y = t.Y,
                GroupColor = t.GroupColor
            });
        }
        ActiveLayout.ZoomLevel = ZoomLevel;
        ActiveLayout.PanX = PanX;
        ActiveLayout.PanY = PanY;
        ActiveLayout.SnapToGrid = SnapToGrid;
        ActiveLayout.GridSize = GridSize;

        MessageLogged?.Invoke(this, $"Layout '{ActiveLayout.Name}' saved");
        StatusText = $"Layout saved: {ActiveLayout.Name}";
    }

    [RelayCommand]
    private void NewLayout()
    {
        var name = $"Layout {SavedLayouts.Count + 1}";
        var layout = new DiagramLayout { Name = name };
        SavedLayouts.Add(layout);
        ActiveLayout = layout;
        MessageLogged?.Invoke(this, $"New layout created: {name}");
    }

    [RelayCommand]
    private void DeleteLayout(DiagramLayout? layout)
    {
        if (layout == null || SavedLayouts.Count <= 1) return;
        SavedLayouts.Remove(layout);
        if (ActiveLayout == layout)
            ActiveLayout = SavedLayouts.FirstOrDefault();
    }

    // === Create Relationship (drag column to column) ===

    [RelayCommand]
    private void CreateRelationship()
    {
        // Placeholder: in the view, drag-from-column will populate these
        MessageLogged?.Invoke(this, "Drag from a column to another table's column to create a relationship.");
    }

    public void CreateRelationship(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        var existing = Relationships.FirstOrDefault(r =>
            r.FromTable == fromTable && r.FromColumn == fromColumn &&
            r.ToTable == toTable && r.ToColumn == toColumn);

        if (existing != null)
        {
            MessageLogged?.Invoke(this, "Relationship already exists.");
            return;
        }

        var rel = new DiagramRelationship
        {
            FromTable = fromTable,
            FromColumn = fromColumn,
            ToTable = toTable,
            ToColumn = toColumn,
            Cardinality = Cardinality.ManyToOne,
            CrossFilterDirection = CrossFilterDirection.Single,
            IsActive = true
        };

        Relationships.Add(rel);
        MessageLogged?.Invoke(this, $"Relationship created: {rel.DisplayName}");
        StatusText = $"{Tables.Count} tables, {Relationships.Count} relationships";
    }

    // === Edit Relationship ===

    [RelayCommand]
    private void DeleteRelationship()
    {
        if (SelectedRelationship == null) return;
        var name = SelectedRelationship.DisplayName;
        Relationships.Remove(SelectedRelationship);
        SelectedRelationship = null;
        MessageLogged?.Invoke(this, $"Relationship deleted: {name}");
        StatusText = $"{Tables.Count} tables, {Relationships.Count} relationships";
    }

    [RelayCommand]
    private void ToggleRelationshipActive()
    {
        if (SelectedRelationship == null) return;
        SelectedRelationship.IsActive = !SelectedRelationship.IsActive;
        MessageLogged?.Invoke(this, $"Relationship {SelectedRelationship.DisplayName}: Active={SelectedRelationship.IsActive}");
    }

    // === Export ===

    [RelayCommand]
    private void ExportDiagramAsImage()
    {
        // This is triggered from view code-behind which captures the visual
        MessageLogged?.Invoke(this, "Use the export button to save diagram as PNG.");
    }

    // === Toggle Minimap ===

    [RelayCommand]
    private void ToggleMinimap()
    {
        ShowMinimap = !ShowMinimap;
    }
}
