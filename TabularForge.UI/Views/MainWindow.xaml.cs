using System.IO;
using System.Windows;
using System.Windows.Interop;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
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

            // Restore window position from settings
            RestoreWindowPosition();
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
            DocumentTabType.TranslationEditor => CreateTranslationEditorPanel(),
            DocumentTabType.PerspectiveEditor => CreatePerspectiveEditorPanel(),
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

    private TranslationEditorPanel CreateTranslationEditorPanel()
    {
        var panel = new TranslationEditorPanel();
        panel.DataContext = _viewModel?.TranslationEditor;
        return panel;
    }

    private PerspectiveEditorPanel CreatePerspectiveEditorPanel()
    {
        var panel = new PerspectiveEditorPanel();
        panel.DataContext = _viewModel?.PerspectiveEditor;
        return panel;
    }

    // ===========================
    //  LAYOUT PERSISTENCE
    // ===========================

    /// <summary>
    /// Saves the current AvalonDock layout to XML string.
    /// </summary>
    public string SerializeLayout()
    {
        try
        {
            var serializer = new XmlLayoutSerializer(DockManager);
            using var writer = new StringWriter();
            serializer.Serialize(writer);
            return writer.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Restores an AvalonDock layout from XML string.
    /// </summary>
    public void DeserializeLayout(string layoutXml)
    {
        if (string.IsNullOrEmpty(layoutXml)) return;

        try
        {
            var serializer = new XmlLayoutSerializer(DockManager);
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                // Skip deserialization of document content - those are dynamic
                if (args.Model is LayoutDocument)
                {
                    args.Cancel = true;
                }
            };
            using var reader = new StringReader(layoutXml);
            serializer.Deserialize(reader);
        }
        catch
        {
            // If layout restore fails, keep the default layout
        }
    }

    private void RestoreWindowPosition()
    {
        if (_viewModel == null) return;

        var settings = _viewModel.SettingsService.Settings;
        if (settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        else if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            WindowState = WindowState.Normal;
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        // Restore saved layout if available
        var layoutXml = _viewModel.SettingsService.LoadLayoutPreset(
            settings.ActiveLayoutPreset);
        if (!string.IsNullOrEmpty(layoutXml))
        {
            DeserializeLayout(layoutXml);
        }
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
        // Save current layout before closing
        if (_viewModel != null)
        {
            var layoutXml = SerializeLayout();
            if (!string.IsNullOrEmpty(layoutXml))
            {
                var settings = _viewModel.SettingsService;
                settings.SaveLayoutPreset(
                    _viewModel.SettingsService.Settings.ActiveLayoutPreset, layoutXml);
            }
        }

        base.OnClosing(e);
    }
}
