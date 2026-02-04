using System.Windows;
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
        mainVm.AddMessage("Phase 3: Connected Features loaded.");
        mainVm.AddMessage("Features: Server Connection, DAX Query, Table Preview, Data Refresh, Deployment.");
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

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ErrorListViewModel>();
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
