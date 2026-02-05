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

        // Load persisted settings
        var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
        var settings = settingsService.Load();

        // Apply persisted theme
        if (!settings.IsDarkTheme)
        {
            mainVm.IsDarkTheme = false;
            mainVm.ApplyTheme();
        }

        mainVm.AddMessage("TabularForge v6.0 started.");
        mainVm.AddMessage($"Runtime: .NET {Environment.Version}");
        mainVm.AddMessage("Phase 6: Polish & Final loaded.");
        mainVm.AddMessage("New: Translation Editor, Perspective Editor, Keyboard Shortcuts, Layout Persistence.");
        mainVm.AddMessage("Previous: C# Script, BPA, Import, Diagram, Pivot, VertiPaq, Server, DAX.");
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

        // Phase 5: Automation & Analysis services
        services.AddSingleton<ScriptingService>();
        services.AddSingleton<BpaService>();
        services.AddSingleton<ImportService>();

        // Phase 6: Polish services
        services.AddSingleton<TranslationService>();
        services.AddSingleton<PerspectiveService>();
        services.AddSingleton<SettingsService>();

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
        if (_serviceProvider != null)
        {
            // Save window state and settings
            var settingsService = _serviceProvider.GetService<SettingsService>();
            var mainVm = _serviceProvider.GetService<MainViewModel>();
            if (settingsService != null && mainVm != null)
            {
                settingsService.Settings.IsDarkTheme = mainVm.IsDarkTheme;
                var mainWindow = Current.MainWindow;
                if (mainWindow != null)
                {
                    settingsService.Settings.IsMaximized = mainWindow.WindowState == System.Windows.WindowState.Maximized;
                    if (mainWindow.WindowState != System.Windows.WindowState.Maximized)
                    {
                        settingsService.Settings.WindowLeft = mainWindow.Left;
                        settingsService.Settings.WindowTop = mainWindow.Top;
                        settingsService.Settings.WindowWidth = mainWindow.Width;
                        settingsService.Settings.WindowHeight = mainWindow.Height;
                    }
                }
                settingsService.Save();
            }

            // Dispose connection service to clean up server connections
            var connectionService = _serviceProvider.GetService<ConnectionService>();
            connectionService?.Dispose();
        }
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
