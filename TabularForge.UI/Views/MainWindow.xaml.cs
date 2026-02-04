using System.Windows;
using AvalonDock;
using AvalonDock.Layout;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = (MainViewModel)DataContext;

        // Subscribe to document tab changes to sync with AvalonDock
        _viewModel.DocumentTabs.CollectionChanged += DocumentTabs_CollectionChanged;

        // Wire up the DockManager active content changed
        DockManager.ActiveContentChanged += DockManager_ActiveContentChanged;
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.DocumentTabs.CollectionChanged += DocumentTabs_CollectionChanged;
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
            _ => new WelcomePanel()
        };
    }

    private DaxQueryPanel CreateDaxQueryPanel()
    {
        var panel = new DaxQueryPanel();
        panel.DataContext = _viewModel.DaxQuery;
        return panel;
    }

    private TablePreviewPanel CreateTablePreviewPanel()
    {
        var panel = new TablePreviewPanel();
        panel.DataContext = _viewModel.TablePreview;
        return panel;
    }

    private DataRefreshPanel CreateDataRefreshPanel()
    {
        var panel = new DataRefreshPanel();
        panel.DataContext = _viewModel.DataRefresh;
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
