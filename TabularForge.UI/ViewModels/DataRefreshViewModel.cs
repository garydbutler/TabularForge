using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class DataRefreshViewModel : ObservableObject
{
    private readonly RefreshService _refreshService;
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    private ObservableCollection<RefreshableObject> _refreshableObjects = new();

    [ObservableProperty]
    private ObservableCollection<RefreshOperation> _activeOperations = new();

    [ObservableProperty]
    private ObservableCollection<RefreshHistoryEntry> _history = new();

    [ObservableProperty]
    private RefreshableObject? _selectedObject;

    [ObservableProperty]
    private RefreshType _selectedRefreshType = RefreshType.Full;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _statusText = "Select objects to refresh.";

    public Array RefreshTypes => Enum.GetValues(typeof(RefreshType));

    public event EventHandler<string>? MessageLogged;

    public DataRefreshViewModel(RefreshService refreshService, ConnectionService connectionService)
    {
        _refreshService = refreshService;
        _connectionService = connectionService;

        _refreshService.RefreshProgressChanged += (_, op) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = ActiveOperations.FirstOrDefault(o => o.ObjectName == op.ObjectName);
                if (existing != null)
                {
                    existing.State = op.State;
                    existing.ProgressPercent = op.ProgressPercent;
                }
                else
                {
                    ActiveOperations.Add(op);
                }
            });
        };

        _refreshService.RefreshCompleted += (_, op) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = ActiveOperations.FirstOrDefault(o => o.ObjectName == op.ObjectName);
                if (existing != null)
                    ActiveOperations.Remove(existing);

                History.Insert(0, new RefreshHistoryEntry
                {
                    ObjectName = op.ObjectName,
                    RefreshType = op.RefreshType,
                    FinalState = op.State,
                    StartTime = op.StartTime,
                    EndTime = op.EndTime ?? DateTime.Now,
                    ErrorMessage = op.ErrorMessage
                });
            });
        };
    }

    [RelayCommand]
    private async Task RefreshSelected()
    {
        if (SelectedObject == null)
        {
            StatusText = "Please select an object to refresh.";
            return;
        }

        if (!_connectionService.IsConnected)
        {
            StatusText = "Not connected to a server.";
            return;
        }

        IsRefreshing = true;
        var cts = _refreshService.CreateCancellationSource();
        StatusText = $"Refreshing {SelectedObject.Name}...";

        try
        {
            var (success, message) = SelectedObject.ObjectType switch
            {
                "Table" => await _refreshService.RefreshTableAsync(
                    SelectedObject.Name, SelectedRefreshType, cts.Token),
                "Partition" => await _refreshService.RefreshPartitionAsync(
                    SelectedObject.ParentName, SelectedObject.Name, SelectedRefreshType, cts.Token),
                "Model" => await _refreshService.RefreshModelAsync(SelectedRefreshType, cts.Token),
                _ => (false, "Unknown object type")
            };

            StatusText = message;
            MessageLogged?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAll()
    {
        if (!_connectionService.IsConnected)
        {
            StatusText = "Not connected to a server.";
            return;
        }

        IsRefreshing = true;
        var cts = _refreshService.CreateCancellationSource();
        StatusText = "Refreshing entire model...";

        try
        {
            var (success, message) = await _refreshService.RefreshModelAsync(SelectedRefreshType, cts.Token);
            StatusText = message;
            MessageLogged?.Invoke(this, message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void CancelRefresh()
    {
        _refreshService.CancelCurrentRefresh();
        StatusText = "Refresh cancellation requested...";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
    }

    public void LoadRefreshableObjects(TomNode? modelRoot)
    {
        RefreshableObjects.Clear();

        if (modelRoot == null) return;

        // Add model itself
        RefreshableObjects.Add(new RefreshableObject
        {
            Name = modelRoot.Name,
            ObjectType = "Model",
            ParentName = ""
        });

        // Add tables and their partitions
        foreach (var child in modelRoot.Children)
        {
            if (child.ObjectType == TomObjectType.Tables)
            {
                foreach (var table in child.Children)
                {
                    RefreshableObjects.Add(new RefreshableObject
                    {
                        Name = table.Name,
                        ObjectType = "Table",
                        ParentName = modelRoot.Name
                    });

                    foreach (var tableChild in table.Children)
                    {
                        if (tableChild.ObjectType == TomObjectType.Partitions)
                        {
                            foreach (var partition in tableChild.Children)
                            {
                                RefreshableObjects.Add(new RefreshableObject
                                {
                                    Name = partition.Name,
                                    ObjectType = "Partition",
                                    ParentName = table.Name
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}

public class RefreshableObject
{
    public string Name { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string DisplayName => ObjectType == "Partition" ? $"  {ParentName} > {Name}" : Name;
}
