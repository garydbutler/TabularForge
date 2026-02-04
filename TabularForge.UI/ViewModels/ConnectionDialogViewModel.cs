using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class ConnectionDialogViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private string _selectedDatabaseName = string.Empty;

    [ObservableProperty]
    private ServerType _serverType = ServerType.SSAS;

    [ObservableProperty]
    private AuthenticationType _authenticationType = AuthenticationType.WindowsIntegrated;

    [ObservableProperty]
    private ObservableCollection<ServerDatabase> _databases = new();

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isBrowsingDatabases;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isServerConnected;

    public bool DialogResult { get; private set; }
    public ConnectionInfo? ResultConnectionInfo { get; private set; }

    public Array ServerTypes => Enum.GetValues(typeof(ServerType));
    public Array AuthenticationTypes => Enum.GetValues(typeof(AuthenticationType));

    public ConnectionDialogViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    [RelayCommand]
    private async Task BrowseDatabases()
    {
        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            StatusMessage = "Please enter a server address.";
            HasError = true;
            return;
        }

        IsBrowsingDatabases = true;
        HasError = false;
        StatusMessage = "Connecting to server...";

        try
        {
            var tempInfo = new ConnectionInfo
            {
                ServerAddress = ServerAddress,
                ServerType = ServerType,
                AuthType = AuthenticationType
            };

            var (success, message) = await _connectionService.ConnectAsync(tempInfo);
            if (!success)
            {
                StatusMessage = message;
                HasError = true;
                return;
            }

            IsServerConnected = true;
            StatusMessage = "Loading databases...";

            var dbs = await _connectionService.GetDatabasesAsync();
            Databases.Clear();
            foreach (var db in dbs)
                Databases.Add(db);

            StatusMessage = $"Found {dbs.Count} database(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBrowsingDatabases = false;
        }
    }

    public bool CanConnect() =>
        !string.IsNullOrWhiteSpace(ServerAddress) && !IsConnecting;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task Connect()
    {
        IsConnecting = true;
        HasError = false;
        StatusMessage = "Connecting...";

        try
        {
            var connectionInfo = new ConnectionInfo
            {
                ServerAddress = ServerAddress,
                DatabaseName = SelectedDatabaseName,
                ServerType = ServerType,
                AuthType = AuthenticationType
            };

            if (!IsServerConnected)
            {
                var (success, message) = await _connectionService.ConnectAsync(connectionInfo);
                if (!success)
                {
                    StatusMessage = message;
                    HasError = true;
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(SelectedDatabaseName))
            {
                var (success, message) = await _connectionService.SelectDatabaseAsync(SelectedDatabaseName);
                if (!success)
                {
                    StatusMessage = message;
                    HasError = true;
                    return;
                }
            }

            ResultConnectionInfo = connectionInfo;
            DialogResult = true;
            StatusMessage = "Connected!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
