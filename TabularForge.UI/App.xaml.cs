using System.Reflection;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;
using TabularForge.Core.Commands;
using TabularForge.Core.Services;
using TabularForge.UI.ViewModels;
using TabularForge.UI.Views;

namespace TabularForge.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register DAX syntax highlighting globally BEFORE any UI is created
        RegisterDaxHighlighting();

        // Configure DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Create and show main window
        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        mainVm.AddMessage("TabularForge started.");
        mainVm.AddMessage($"Runtime: .NET {Environment.Version}");
        mainVm.AddMessage("Phase 4: Visual Tools loaded.");
        mainVm.AddMessage("Features: Diagram View, Pivot Grid, VertiPaq Analyzer.");
        mainVm.AddMessage("Previous: Server Connection, DAX Query, Table Preview, Data Refresh, Deployment.");
        mainVm.AddMessage("Ready. Open a .bim file or connect to a server to begin.");

        mainWindow.Show();

        // If a file was passed as command-line argument, open it
        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
        {
            mainVm.LoadFile(e.Args[0]);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<BimFileService>();
        services.AddSingleton<UndoRedoManager>();

        // Phase 3: Connected services
        services.AddSingleton<ConnectionService>();
        services.AddSingleton<QueryService>();
        services.AddSingleton<RefreshService>();
        services.AddSingleton<DeploymentService>();

        // Phase 4: Visual Tools services
        services.AddSingleton<DiagramService>();
        services.AddSingleton<VertiPaqService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ErrorListViewModel>();
    }

    private static void RegisterDaxHighlighting()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TabularForge.UI.SyntaxHighlighting.DAX.xshd";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting(
                    "DAX", new[] { ".dax", ".msdax" }, definition);
            }
            else
            {
                // Fallback: try loading from file next to exe
                var dir = System.IO.Path.GetDirectoryName(assembly.Location) ?? ".";
                var xshdPath = System.IO.Path.Combine(dir, "SyntaxHighlighting", "DAX.xshd");
                if (System.IO.File.Exists(xshdPath))
                {
                    using var fileStream = System.IO.File.OpenRead(xshdPath);
                    using var reader = new XmlTextReader(fileStream);
                    var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting(
                        "DAX", new[] { ".dax", ".msdax" }, definition);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DAX highlighting registration failed: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Dispose connection service to clean up server connections
        if (_serviceProvider != null)
        {
            var connectionService = _serviceProvider.GetService<ConnectionService>();
            connectionService?.Dispose();
        }
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
