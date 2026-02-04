using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Commands;
using TabularForge.Core.Models;
using TabularForge.Core.Services;
using TabularForge.DAXParser.Semantics;
using TabularForge.UI.Views;

namespace TabularForge.UI.ViewModels;

/// <summary>
/// Main ViewModel for the application. Coordinates all panels and commands.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BimFileService _bimFileService;
    private readonly UndoRedoManager _undoRedoManager;
    private readonly ConnectionService _connectionService;
    private readonly QueryService _queryService;
    private readonly RefreshService _refreshService;
    private readonly DeploymentService _deploymentService;
    private readonly DiagramService _diagramService;
    private readonly VertiPaqService _vertiPaqService;
    private string? _currentFilePath;

    // === Model State ===

    [ObservableProperty]
    private TomNode? _modelRoot;

    [ObservableProperty]
    private TomNode? _selectedNode;

    [ObservableProperty]
    private string _windowTitle = "TabularForge";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isModelLoaded;

    // === Status Bar ===

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _modelName = "No model loaded";

    [ObservableProperty]
    private string _objectCount = "0 objects";

    [ObservableProperty]
    private string _cursorPosition = "Ln 1, Col 1";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // === Search ===

    [ObservableProperty]
    private string _searchText = string.Empty;

    // === Messages/Output ===

    [ObservableProperty]
    private string _messagesText = string.Empty;

    [ObservableProperty]
    private string _outputText = string.Empty;

    // === DAX Editor ===

    [ObservableProperty]
    private string _daxEditorContent = string.Empty;

    [ObservableProperty]
    private string _daxEditorTitle = "DAX Editor";

    // === Properties Panel ===

    [ObservableProperty]
    private ObservableCollection<TomProperty> _selectedProperties = new();

    [ObservableProperty]
    private string _propertiesHeader = "Properties";

    // === Document Tabs ===

    [ObservableProperty]
    private ObservableCollection<DocumentTabViewModel> _documentTabs = new();

    [ObservableProperty]
    private DocumentTabViewModel? _activeDocument;

    // === Error List (Phase 2) ===

    [ObservableProperty]
    private ErrorListViewModel _errorList = new();

    // === Phase 3: Connected Feature ViewModels ===

    [ObservableProperty]
    private DaxQueryViewModel? _daxQuery;

    [ObservableProperty]
    private TablePreviewViewModel? _tablePreview;

    [ObservableProperty]
    private DataRefreshViewModel? _dataRefresh;

    [ObservableProperty]
    private bool _isServerConnected;

    // === Phase 4: Visual Tools ViewModels ===

    [ObservableProperty]
    private DiagramViewModel? _diagram;

    [ObservableProperty]
    private PivotGridViewModel? _pivotGrid;

    [ObservableProperty]
    private VertiPaqViewModel? _vertiPaq;

    // === Undo/Redo ===

    public UndoRedoManager UndoRedo => _undoRedoManager;

    public bool CanUndo => _undoRedoManager.CanUndo;
    public bool CanRedo => _undoRedoManager.CanRedo;
    public string UndoDescription => _undoRedoManager.UndoDescription;
    public string RedoDescription => _undoRedoManager.RedoDescription;

    public MainViewModel(
        BimFileService bimFileService,
        UndoRedoManager undoRedoManager,
        ConnectionService connectionService,
        QueryService queryService,
        RefreshService refreshService,
        DeploymentService deploymentService,
        DiagramService diagramService,
        VertiPaqService vertiPaqService)
    {
        _bimFileService = bimFileService;
        _undoRedoManager = undoRedoManager;
        _connectionService = connectionService;
        _queryService = queryService;
        _refreshService = refreshService;
        _deploymentService = deploymentService;
        _diagramService = diagramService;
        _vertiPaqService = vertiPaqService;

        // Initialize Phase 3 sub-ViewModels
        DaxQuery = new DaxQueryViewModel(queryService, connectionService);
        TablePreview = new TablePreviewViewModel(queryService, connectionService);
        DataRefresh = new DataRefreshViewModel(refreshService, connectionService);

        // Initialize Phase 4 sub-ViewModels
        Diagram = new DiagramViewModel(diagramService, connectionService);
        PivotGrid = new PivotGridViewModel(queryService, connectionService);
        VertiPaq = new VertiPaqViewModel(vertiPaqService, connectionService);

        // Wire message logging from sub-VMs
        DaxQuery.MessageLogged += (_, msg) => AddMessage(msg);
        TablePreview.MessageLogged += (_, msg) => AddMessage(msg);
        DataRefresh.MessageLogged += (_, msg) => AddMessage(msg);
        Diagram.MessageLogged += (_, msg) => AddMessage(msg);
        PivotGrid.MessageLogged += (_, msg) => AddMessage(msg);
        VertiPaq.MessageLogged += (_, msg) => AddMessage(msg);

        // Track connection state changes
        _connectionService.ConnectionStateChanged += (_, connected) =>
        {
            IsServerConnected = connected;
            ConnectionStatus = connected
                ? $"Connected: {_connectionService.CurrentConnection?.DisplayName}"
                : "Disconnected";

            if (connected)
                RefreshTableLists();
        };

        _undoRedoManager.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoDescription));
            OnPropertyChanged(nameof(RedoDescription));
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
    }

    // ===========================
    //  FILE COMMANDS
    // ===========================

    [RelayCommand]
    private void NewModel()
    {
        ModelRoot = null;
        _currentFilePath = null;
        IsModelLoaded = false;
        SelectedNode = null;
        SelectedProperties.Clear();
        DaxEditorContent = string.Empty;
        DocumentTabs.Clear();
        _undoRedoManager.Clear();
        ErrorList.Clear();
        WindowTitle = "TabularForge - New Model";
        ModelName = "New Model";
        ObjectCount = "0 objects";
        AddMessage("New model created.");
    }

    [RelayCommand]
    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Tabular Model",
            Filter = "BIM Files (*.bim)|*.bim|All Files (*.*)|*.*",
            DefaultExt = ".bim"
        };

        if (dlg.ShowDialog() != true) return;

        LoadFile(dlg.FileName);
    }

    public void LoadFile(string filePath)
    {
        try
        {
            StatusMessage = $"Loading {Path.GetFileName(filePath)}...";
            var root = _bimFileService.LoadBimFile(filePath);
            ModelRoot = root;
            _currentFilePath = filePath;
            IsModelLoaded = true;
            _undoRedoManager.Clear();

            var count = BimFileService.CountObjects(root);
            ObjectCount = $"{count} objects";
            ModelName = root.Name;
            WindowTitle = $"TabularForge - {Path.GetFileName(filePath)}";
            ConnectionStatus = "File";
            StatusMessage = $"Loaded {Path.GetFileName(filePath)} ({count} objects)";

            AddMessage($"Opened: {filePath}");
            AddMessage($"Model: {root.Name} | {count} objects");

            // Update refresh view with refreshable objects
            DataRefresh?.LoadRefreshableObjects(root);

            // Update Phase 4 panels with model data
            Diagram?.LoadFromModel(root);
            PivotGrid?.LoadFieldsFromModel(root);

            // Auto-select the model root
            SelectedNode = root;
        }
        catch (Exception ex)
        {
            StatusMessage = "Error loading file";
            AddMessage($"ERROR: Failed to load file: {ex.Message}");
            MessageBox.Show($"Failed to load file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (_currentFilePath == null)
        {
            SaveFileAs();
            return;
        }

        DoSave(_currentFilePath);
    }

    [RelayCommand]
    private void SaveFileAs()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save Tabular Model As",
            Filter = "BIM Files (*.bim)|*.bim|All Files (*.*)|*.*",
            DefaultExt = ".bim"
        };

        if (dlg.ShowDialog() != true) return;

        DoSave(dlg.FileName);
    }

    private void DoSave(string filePath)
    {
        if (ModelRoot == null) return;

        try
        {
            _bimFileService.SaveBimFile(filePath, ModelRoot);
            _currentFilePath = filePath;
            WindowTitle = $"TabularForge - {Path.GetFileName(filePath)}";
            StatusMessage = $"Saved to {Path.GetFileName(filePath)}";
            AddMessage($"Saved: {filePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Error saving file";
            AddMessage($"ERROR: Failed to save file: {ex.Message}");
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===========================
    //  EDIT COMMANDS
    // ===========================

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undoRedoManager.Undo();
        AddMessage($"Undo: {_undoRedoManager.RedoDescription}");
        RefreshSelectedProperties();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undoRedoManager.Redo();
        AddMessage($"Redo: {_undoRedoManager.UndoDescription}");
        RefreshSelectedProperties();
    }

    // ===========================
    //  TREE SELECTION
    // ===========================

    partial void OnSelectedNodeChanged(TomNode? value)
    {
        if (value == null)
        {
            SelectedProperties.Clear();
            PropertiesHeader = "Properties";
            return;
        }

        PropertiesHeader = $"Properties - {value.Name}";
        value.BuildProperties();
        SelectedProperties = new ObservableCollection<TomProperty>(value.Properties);

        // If it's a measure or calculated column, show expression in DAX editor
        if (value.ObjectType is TomObjectType.Measure or TomObjectType.CalculatedColumn
            or TomObjectType.CalculatedTableColumn)
        {
            DaxEditorContent = value.Expression;
            DaxEditorTitle = $"DAX Editor - {value.Name}";

            // Open or focus the DAX editor tab
            var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == $"dax_{value.GetPath()}");
            if (existingTab != null)
            {
                ActiveDocument = existingTab;
            }
            else
            {
                var tab = new DocumentTabViewModel
                {
                    Title = value.Name,
                    ContentId = $"dax_{value.GetPath()}",
                    TabType = DocumentTabType.DaxEditor,
                    Content = value.Expression,
                    AssociatedNode = value
                };
                DocumentTabs.Add(tab);
                ActiveDocument = tab;
            }
        }
    }

    private void RefreshSelectedProperties()
    {
        if (SelectedNode == null) return;
        SelectedNode.BuildProperties();
        SelectedProperties = new ObservableCollection<TomProperty>(SelectedNode.Properties);
    }

    // ===========================
    //  SEARCH / FILTER
    // ===========================

    partial void OnSearchTextChanged(string value)
    {
        if (ModelRoot == null) return;
        BimFileService.FilterTree(ModelRoot, value);
    }

    // ===========================
    //  THEME
    // ===========================

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        var app = Application.Current;
        var newTheme = IsDarkTheme
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        // Replace the first resource dictionary (theme)
        if (app.Resources.MergedDictionaries.Count > 0)
        {
            app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = newTheme };
        }
        else
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = newTheme });
        }

        AddMessage($"Theme changed to {(IsDarkTheme ? "Dark" : "Light")}");
    }

    // ===========================
    //  CONTEXT MENU COMMANDS
    // ===========================

    [RelayCommand]
    private void RenameNode()
    {
        if (SelectedNode == null) return;
        AddMessage($"Rename: {SelectedNode.Name} (use Properties panel to change name)");
    }

    [RelayCommand]
    private void DeleteNode()
    {
        if (SelectedNode == null || SelectedNode.Parent == null) return;
        var name = SelectedNode.Name;
        var result = MessageBox.Show($"Delete '{name}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        SelectedNode.Parent.Children.Remove(SelectedNode);
        SelectedNode = null;
        if (ModelRoot != null)
            ObjectCount = $"{BimFileService.CountObjects(ModelRoot)} objects";
        AddMessage($"Deleted: {name}");
    }

    [RelayCommand]
    private void DuplicateNode()
    {
        if (SelectedNode?.Parent == null) return;
        AddMessage($"Duplicate: {SelectedNode.Name} (not yet implemented in Phase 1)");
    }

    [RelayCommand]
    private void ToggleHidden()
    {
        if (SelectedNode == null) return;
        var cmd = new PropertyChangeCommand(SelectedNode, "IsHidden",
            SelectedNode.IsHidden, !SelectedNode.IsHidden);
        _undoRedoManager.Execute(cmd);
        AddMessage($"{SelectedNode.Name} is now {(SelectedNode.IsHidden ? "hidden" : "visible")}");
        RefreshSelectedProperties();
    }

    [RelayCommand]
    private void ShowDependencies()
    {
        if (SelectedNode == null) return;
        AddMessage($"Dependencies for {SelectedNode.Name}: (Phase 3 feature)");
    }

    // ===========================
    //  VIEW COMMANDS
    // ===========================

    [RelayCommand]
    private void ExpandAll()
    {
        if (ModelRoot == null) return;
        SetExpandedRecursive(ModelRoot, true);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        if (ModelRoot == null) return;
        SetExpandedRecursive(ModelRoot, false);
    }

    private void SetExpandedRecursive(TomNode node, bool expanded)
    {
        node.IsExpanded = expanded;
        foreach (var child in node.Children)
            SetExpandedRecursive(child, expanded);
    }

    // ===========================
    //  DAX EDITOR
    // ===========================

    [RelayCommand]
    private void ApplyDaxExpression()
    {
        if (SelectedNode == null) return;
        if (SelectedNode.ObjectType is not (TomObjectType.Measure or TomObjectType.CalculatedColumn
            or TomObjectType.CalculatedTableColumn))
            return;

        var oldExpression = SelectedNode.Expression;
        var newExpression = ActiveDocument?.Content ?? DaxEditorContent;

        if (oldExpression == newExpression) return;

        var cmd = new PropertyChangeCommand(SelectedNode, "Expression", oldExpression, newExpression);
        _undoRedoManager.Execute(cmd);
        AddMessage($"Updated expression for {SelectedNode.Name}");
        RefreshSelectedProperties();
    }

    // ===========================
    //  DAX SEMANTIC CHECK (Phase 2)
    // ===========================

    [RelayCommand]
    private void CheckDaxSemantics()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(DaxEditorContent)) return;

        var modelInfo = BuildModelInfo();
        var analyzer = new DaxSemanticAnalyzer(modelInfo);
        var diagnostics = analyzer.Analyze(DaxEditorContent, SelectedNode.Name);

        ErrorList.UpdateDiagnostics(diagnostics);
        var errorCount = diagnostics.Count(d => d.Severity == DaxDiagnosticSeverity.Error);
        var warnCount = diagnostics.Count(d => d.Severity == DaxDiagnosticSeverity.Warning);
        AddMessage($"Semantic check for '{SelectedNode.Name}': {errorCount} errors, {warnCount} warnings");
    }

    // ===========================
    //  DAX SCRIPTING (Phase 2)
    // ===========================

    [RelayCommand]
    private void OpenDaxScript()
    {
        if (ModelRoot == null)
        {
            AddMessage("No model loaded. Open a .bim file first.");
            return;
        }

        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "dax_script");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "DAX Script",
            ContentId = "dax_script",
            TabType = DocumentTabType.DaxScript,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;
        AddMessage("DAX Scripting panel opened.");
    }

    // ===========================
    //  PHASE 3: SERVER CONNECTION
    // ===========================

    [RelayCommand]
    private void ConnectToServer()
    {
        var dialogVm = new ConnectionDialogViewModel(_connectionService);
        var dialog = new ConnectionDialog(dialogVm)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            AddMessage($"Connected to {dialogVm.ResultConnectionInfo?.DisplayName}");
            StatusMessage = $"Connected to {dialogVm.ResultConnectionInfo?.DisplayName}";

            // Refresh table lists for preview and refresh panels
            RefreshTableLists();
            DataRefresh?.LoadRefreshableObjects(ModelRoot);
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        if (!_connectionService.IsConnected) return;

        await _connectionService.DisconnectAsync();
        ConnectionStatus = "Disconnected";
        IsServerConnected = false;
        AddMessage("Disconnected from server.");
        StatusMessage = "Disconnected";
    }

    private void RefreshTableLists()
    {
        if (ModelRoot == null) return;

        var tableNames = new List<string>();
        CollectTableNames(ModelRoot, tableNames);
        TablePreview?.RefreshTableList(tableNames);
    }

    private void CollectTableNames(TomNode node, List<string> tableNames)
    {
        if (node.ObjectType == TomObjectType.Table)
            tableNames.Add(node.Name);
        foreach (var child in node.Children)
            CollectTableNames(child, tableNames);
    }

    // ===========================
    //  PHASE 3: DAX QUERY
    // ===========================

    [RelayCommand]
    private void OpenDaxQuery()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "dax_query");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "DAX Query",
            ContentId = "dax_query",
            TabType = DocumentTabType.DaxQuery,
            Content = "EVALUATE\n\n"
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;
        AddMessage("DAX Query panel opened.");
    }

    // ===========================
    //  PHASE 3: TABLE PREVIEW
    // ===========================

    [RelayCommand]
    private void OpenTablePreview()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "table_preview");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "Table Preview",
            ContentId = "table_preview",
            TabType = DocumentTabType.TablePreview,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;
        AddMessage("Table Preview panel opened.");

        // Populate table list if model is loaded
        RefreshTableLists();
    }

    // ===========================
    //  PHASE 3: DATA REFRESH
    // ===========================

    [RelayCommand]
    private void OpenDataRefresh()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "data_refresh");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "Data Refresh",
            ContentId = "data_refresh",
            TabType = DocumentTabType.DataRefresh,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;
        DataRefresh?.LoadRefreshableObjects(ModelRoot);
        AddMessage("Data Refresh panel opened.");
    }

    // ===========================
    //  PHASE 3: DEPLOYMENT WIZARD
    // ===========================

    [RelayCommand]
    private void OpenDeploymentWizard()
    {
        if (ModelRoot == null)
        {
            AddMessage("No model loaded. Open a .bim file first.");
            MessageBox.Show("No model is loaded. Please open a .bim file first.",
                "Deployment", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var wizardVm = new DeploymentWizardViewModel(_deploymentService, _connectionService);
        wizardVm.MessageLogged += (_, msg) => AddMessage(msg);
        wizardVm.Initialize(ModelRoot);

        var dialog = new DeploymentWizardDialog
        {
            DataContext = wizardVm,
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        if (wizardVm.DeploymentSucceeded)
        {
            AddMessage("Deployment completed successfully.");
            StatusMessage = "Deployment completed";
        }
    }

    // ===========================
    //  PHASE 4: DIAGRAM VIEW
    // ===========================

    [RelayCommand]
    private void OpenDiagram()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "diagram_view");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "Diagram",
            ContentId = "diagram_view",
            TabType = DocumentTabType.Diagram,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;

        // Load diagram from model if available
        Diagram?.LoadFromModel(ModelRoot);
        AddMessage("Diagram View opened.");
    }

    // ===========================
    //  PHASE 4: PIVOT GRID
    // ===========================

    [RelayCommand]
    private void OpenPivotGrid()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "pivot_grid");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "Pivot Grid",
            ContentId = "pivot_grid",
            TabType = DocumentTabType.PivotGrid,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;

        // Load fields from model
        PivotGrid?.LoadFieldsFromModel(ModelRoot);
        AddMessage("Pivot Grid opened.");
    }

    // ===========================
    //  PHASE 4: VERTIPAQ ANALYZER
    // ===========================

    [RelayCommand]
    private void OpenVertiPaq()
    {
        var existingTab = DocumentTabs.FirstOrDefault(t => t.ContentId == "vertipaq_analyzer");
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        var tab = new DocumentTabViewModel
        {
            Title = "VertiPaq Analyzer",
            ContentId = "vertipaq_analyzer",
            TabType = DocumentTabType.VertiPaq,
            Content = string.Empty
        };
        DocumentTabs.Add(tab);
        ActiveDocument = tab;
        AddMessage("VertiPaq Analyzer opened.");
    }

    // ===========================
    //  MODEL INFO (Phase 2)
    // ===========================

    public ModelInfo BuildModelInfo()
    {
        var info = new ModelInfo();
        if (ModelRoot == null) return info;

        CollectModelInfo(ModelRoot, info);
        return info;
    }

    private void CollectModelInfo(TomNode node, ModelInfo info)
    {
        if (node.ObjectType == TomObjectType.Table)
        {
            var tableInfo = new TableInfo { Name = node.Name };

            foreach (var child in node.Children)
            {
                CollectTableMembers(child, tableInfo);
            }

            info.Tables.Add(tableInfo);
        }

        foreach (var child in node.Children)
        {
            CollectModelInfo(child, info);
        }
    }

    private void CollectTableMembers(TomNode node, TableInfo tableInfo)
    {
        switch (node.ObjectType)
        {
            case TomObjectType.DataColumn:
            case TomObjectType.Column:
            case TomObjectType.CalculatedColumn:
            case TomObjectType.CalculatedTableColumn:
                tableInfo.Columns.Add(new ColumnInfo
                {
                    Name = node.Name,
                    DataType = node.DataType,
                    TableName = tableInfo.Name
                });
                break;

            case TomObjectType.Measure:
                tableInfo.Measures.Add(new MeasureInfo
                {
                    Name = node.Name,
                    Expression = node.Expression,
                    TableName = tableInfo.Name
                });
                break;
        }

        foreach (var child in node.Children)
        {
            CollectTableMembers(child, tableInfo);
        }
    }

    // ===========================
    //  MESSAGES
    // ===========================

    public void AddMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        MessagesText += $"[{timestamp}] {message}\n";
    }

    public void AddOutput(string output)
    {
        OutputText += output + "\n";
    }

    // ===========================
    //  ABOUT
    // ===========================

    [RelayCommand]
    private void ShowAbout()
    {
        MessageBox.Show(
            "TabularForge v4.0.0\n\n" +
            "A Tabular Model Editor for Power BI and Analysis Services\n\n" +
            "Built with .NET 8, WPF, AvalonDock, and AvalonEdit\n\n" +
            "Phase 4: Visual Tools\n" +
            "- Diagram View (Relationship Editor)\n" +
            "- Pivot Grid\n" +
            "- VertiPaq Analyzer\n\n" +
            "Previous: Server Connection, DAX Query, Table Preview,\n" +
            "Data Refresh, Deployment Wizard",
            "About TabularForge",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

// ===========================
//  Document Tab ViewModel
// ===========================

public enum DocumentTabType
{
    DaxEditor,
    DaxQuery,
    DaxScript,
    Diagram,
    TablePreview,
    DataRefresh,
    PivotGrid,
    VertiPaq,
    Welcome
}

public partial class DocumentTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    private string _contentId = string.Empty;

    [ObservableProperty]
    private DocumentTabType _tabType;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private TomNode? _associatedNode;

    public string DisplayTitle => IsModified ? $"{Title} *" : Title;

    partial void OnIsModifiedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
