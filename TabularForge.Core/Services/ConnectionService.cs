using System.Data;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.Identity.Client;
using TabularForge.Core.Models;
using TomDatabase = Microsoft.AnalysisServices.Tabular.Database;
using TomServer = Microsoft.AnalysisServices.Tabular.Server;

namespace TabularForge.Core.Services;

public class ConnectionService : IDisposable
{
    private TomServer? _server;
    private TomDatabase? _database;
    private ConnectionInfo? _currentConnection;
    private bool _isConnected;

    // MSAL config for Azure AD
    private const string AzureAdClientId = "a672d62c-fc7b-4e81-a576-e60dc46e951d"; // common Power BI client
    private const string AzureAdAuthority = "https://login.microsoftonline.com/common";
    private const string RedirectUri = "http://localhost";
    private static readonly string[] Scopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    // Persist MSAL app instance for token caching across calls
    private IPublicClientApplication? _msalApp;
    private IntPtr _parentWindowHandle;

    /// <summary>
    /// Set the parent window handle for MSAL interactive auth popups.
    /// Call this from your MainWindow after it loads.
    /// </summary>
    public void SetParentWindow(IntPtr windowHandle)
    {
        _parentWindowHandle = windowHandle;
    }

    public bool IsConnected => _isConnected;
    public ConnectionInfo? CurrentConnection => _currentConnection;
    public TomDatabase? Database => _database;
    public TomServer? Server => _server;

    public event EventHandler<bool>? ConnectionStateChanged;

    public async Task<(bool Success, string Message)> ConnectAsync(ConnectionInfo connectionInfo)
    {
        try
        {
            await DisconnectAsync();

            _server = new TomServer();
            var connectionString = await BuildConnectionStringAsync(connectionInfo);

            await Task.Run(() => _server.Connect(connectionString));

            if (!string.IsNullOrEmpty(connectionInfo.DatabaseName))
            {
                _database = _server.Databases.FindByName(connectionInfo.DatabaseName);
                if (_database == null)
                    return (false, $"Database '{connectionInfo.DatabaseName}' not found on server.");
            }
            else if (_server.Databases.Count > 0)
            {
                _database = _server.Databases[0];
                connectionInfo.DatabaseName = _database.Name;
            }

            _currentConnection = connectionInfo;
            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            return (true, $"Connected to {connectionInfo.DisplayName}");
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _server?.Dispose();
            _server = null;
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_server != null)
        {
            await Task.Run(() =>
            {
                if (_server.Connected)
                    _server.Disconnect();
                _server.Dispose();
            });
            _server = null;
        }
        _database = null;
        _currentConnection = null;
        _isConnected = false;
        ConnectionStateChanged?.Invoke(this, false);
    }

    public async Task<List<ServerDatabase>> GetDatabasesAsync()
    {
        if (_server == null || !_server.Connected)
            return new List<ServerDatabase>();

        return await Task.Run(() =>
        {
            var databases = new List<ServerDatabase>();
            foreach (TomDatabase db in _server.Databases)
            {
                databases.Add(new ServerDatabase
                {
                    Name = db.Name,
                    Id = db.ID,
                    EstimatedSize = db.EstimatedSize,
                    CompatibilityLevel = db.CompatibilityLevel.ToString(),
                    LastProcessed = db.LastProcessed
                });
            }
            return databases;
        });
    }

    public async Task<(bool Success, string Message)> SelectDatabaseAsync(string databaseName)
    {
        if (_server == null || !_server.Connected)
            return (false, "Not connected to a server.");

        return await Task.Run(() =>
        {
            _database = _server.Databases.FindByName(databaseName);
            if (_database == null)
                return (false, $"Database '{databaseName}' not found.");

            if (_currentConnection != null)
                _currentConnection.DatabaseName = databaseName;

            return (true, $"Selected database: {databaseName}");
        });
    }

    public Model? GetTomModel()
    {
        return _database?.Model;
    }

    private async Task<string> BuildConnectionStringAsync(ConnectionInfo info)
    {
        var server = info.ServerAddress;

        return info.AuthType switch
        {
            AuthenticationType.WindowsIntegrated =>
                $"Provider=MSOLAP;Data Source={server};Integrated Security=SSPI;",
            AuthenticationType.AzureAD =>
                $"Provider=MSOLAP;Data Source={server};Password={await GetAzureAdTokenAsync()};Persist Security Info=True;Impersonation Level=Impersonate;",
            AuthenticationType.UsernamePassword =>
                $"Provider=MSOLAP;Data Source={server};User ID={info.Username};Password={info.Password};Persist Security Info=True;",
            _ => $"Provider=MSOLAP;Data Source={server};Integrated Security=SSPI;"
        };
    }

    private IPublicClientApplication GetOrCreateMsalApp()
    {
        if (_msalApp == null)
        {
            _msalApp = PublicClientApplicationBuilder
                .Create(AzureAdClientId)
                .WithAuthority(AzureAdAuthority)
                .WithRedirectUri(RedirectUri)
                .Build();
        }
        return _msalApp;
    }

    private async Task<string> GetAzureAdTokenAsync()
    {
        var app = GetOrCreateMsalApp();
        AuthenticationResult? result = null;

        try
        {
            // Try silent auth first (cached token)
            var accounts = await app.GetAccountsAsync();
            if (accounts.Any())
            {
                result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            else
            {
                throw new MsalUiRequiredException("no_account", "No cached account found.");
            }
        }
        catch (MsalUiRequiredException)
        {
            // Interactive login required
            var builder = app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false); // Use system browser for reliability

            // Set parent window so focus returns to app after browser auth
            if (_parentWindowHandle != IntPtr.Zero)
            {
                builder = builder.WithParentActivityOrWindow(_parentWindowHandle);
            }

            result = await builder.ExecuteAsync();
        }

        return result.AccessToken;
    }

    public void Dispose()
    {
        if (_server != null)
        {
            if (_server.Connected)
                _server.Disconnect();
            _server.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
