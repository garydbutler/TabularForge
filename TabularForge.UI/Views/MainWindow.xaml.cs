using System.Windows;
using System.Windows.Interop;
using AvalonDock;
using AvalonDock.Layout;
using TabularForge.Core.Services;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up the DockManager active content changed
        DockManager.ActiveContentChanged += DockManager_ActiveContentChanged;

        // Subscribe to DataContext changes to wire up ViewModel when it's set
        DataContextChanged += MainWindow_DataContextChanged;

        // Pass window handle to ConnectionService for MSAL auth popups
        Loaded += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (_viewModel != null)
            {
                var connectionService = GetConnectionService();
                connectionService?.SetParentWindow(handle);
            }
        };
    }

    private ConnectionService? GetConnectionService()
    {
        // Access via DI - the MainViewModel has a reference
        return _viewModel?.ConnectionService;
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe from old ViewModel if any
        if (_viewModel != null)
        {
            _viewModel.DocumentTabs.CollectionChanged -= DocumentTabs_CollectionChanged;
        }

        // Subscribe to new ViewModel
        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.DocumentTabs.CollectionChanged += DocumentTabs_CollectionChanged;
        }
    }

    private void DocumentTabs_CollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null) return;

        foreach (DocumentTabViewModel tab in e.NewItems)
        {
            var layoutDoc = new LayoutDocument
            {
                Title = tab.DisplayTitle,
                ContentId = tab.ContentId,
                Content = CreateDocumentContent(tab)
            };

            DocumentPane.Children.Add(layoutDoc);
            DocumentPane.SelectedContentIndex = DocumentPane.Children.Count - 1;
        }
    }

    private object CreateDocumentContent(DocumentTabViewModel tab)
    {
        return tab.TabType switch
        {
            DocumentTabType.DaxEditor => new DaxEditorPanel(),
            DocumentTabType.DaxScript => new DaxScriptingPanel(),
            DocumentTabType.DaxQuery => CreateDaxQueryPanel(),
            DocumentTabType.TablePreview => CreateTablePreviewPanel(),
            DocumentTabType.DataRefresh => CreateDataRefreshPanel(),
            DocumentTabType.Diagram => CreateDiagramPanel(),
            DocumentTabType.PivotGrid => CreatePivotGridPanel(),
            DocumentTabType.VertiPaq => CreateVertiPaqPanel(),
            DocumentTabType.CSharpScript => CreateCSharpScriptPanel(),
            DocumentTabType.BestPracticeAnalyzer => CreateBpaPanel(),
            _ => new WelcomePanel()
        };
    }

    private DaxQueryPanel CreateDaxQueryPanel()
    {
        var panel = new DaxQueryPanel();
        panel.DataContext = _viewModel?.DaxQuery;
        return panel;
    }

    private TablePreviewPanel CreateTablePreviewPanel()
    {
        var panel = new TablePreviewPanel();
        panel.DataContext = _viewModel?.TablePreview;
        return panel;
    }

    private DataRefreshPanel CreateDataRefreshPanel()
    {
        var panel = new DataRefreshPanel();
        panel.DataContext = _viewModel?.DataRefresh;
        return panel;
    }

    private DiagramPanel CreateDiagramPanel()
    {
        var panel = new DiagramPanel();
        panel.DataContext = _viewModel?.Diagram;
        return panel;
    }

    private PivotGridPanel CreatePivotGridPanel()
    {
        var panel = new PivotGridPanel();
        panel.DataContext = _viewModel?.PivotGrid;
        return panel;
    }

    private VertiPaqPanel CreateVertiPaqPanel()
    {
        var panel = new VertiPaqPanel();
        panel.DataContext = _viewModel?.VertiPaq;
        return panel;
    }

    private CSharpScriptPanel CreateCSharpScriptPanel()
    {
        var panel = new CSharpScriptPanel();
        panel.DataContext = _viewModel?.ScriptEditor;
        return panel;
    }

    private BpaPanel CreateBpaPanel()
    {
        var panel = new BpaPanel();
        panel.DataContext = _viewModel?.Bpa;
        return panel;
    }

    private void DockManager_ActiveContentChanged(object? sender, EventArgs e)
    {
        // Keep view model in sync with AvalonDock's active document
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
    }
}
