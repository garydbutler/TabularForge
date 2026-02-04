using Microsoft.AnalysisServices.Tabular;
using TabularForge.Core.Models;
using TomRefreshType = Microsoft.AnalysisServices.Tabular.RefreshType;
using ModelRefreshType = TabularForge.Core.Models.RefreshType;

namespace TabularForge.Core.Services;

public class RefreshService
{
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler<RefreshOperation>? RefreshProgressChanged;
    public event EventHandler<RefreshOperation>? RefreshCompleted;

    public RefreshService(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<(bool Success, string Message)> RefreshTableAsync(
        string tableName, ModelRefreshType refreshType, CancellationToken cancellationToken = default)
    {
        var model = _connectionService.GetTomModel();
        if (model == null)
            return (false, "No model connected.");

        var operation = new RefreshOperation
        {
            ObjectName = tableName,
            ObjectType = "Table",
            RefreshType = refreshType,
            State = RefreshState.InProgress,
            StartTime = DateTime.Now
        };

        RefreshProgressChanged?.Invoke(this, operation);

        try
        {
            var table = model.Tables.Find(tableName);
            if (table == null)
                return (false, $"Table '{tableName}' not found in model.");

            var tomRefreshType = MapRefreshType(refreshType);

            await Task.Run(() =>
            {
                table.RequestRefresh(tomRefreshType);
                model.SaveChanges();
            }, cancellationToken);

            operation.State = RefreshState.Completed;
            operation.EndTime = DateTime.Now;
            operation.ProgressPercent = 100;
            RefreshCompleted?.Invoke(this, operation);

            return (true, $"Refresh completed for table '{tableName}'.");
        }
        catch (OperationCanceledException)
        {
            operation.State = RefreshState.Cancelled;
            operation.EndTime = DateTime.Now;
            operation.ErrorMessage = "Refresh was cancelled.";
            RefreshCompleted?.Invoke(this, operation);
            return (false, "Refresh was cancelled.");
        }
        catch (Exception ex)
        {
            operation.State = RefreshState.Failed;
            operation.EndTime = DateTime.Now;
            operation.ErrorMessage = ex.Message;
            RefreshCompleted?.Invoke(this, operation);
            return (false, $"Refresh failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RefreshPartitionAsync(
        string tableName, string partitionName, ModelRefreshType refreshType, CancellationToken cancellationToken = default)
    {
        var model = _connectionService.GetTomModel();
        if (model == null)
            return (false, "No model connected.");

        try
        {
            var table = model.Tables.Find(tableName);
            if (table == null)
                return (false, $"Table '{tableName}' not found.");

            var partition = table.Partitions.Find(partitionName);
            if (partition == null)
                return (false, $"Partition '{partitionName}' not found in table '{tableName}'.");

            var tomRefreshType = MapRefreshType(refreshType);

            await Task.Run(() =>
            {
                partition.RequestRefresh(tomRefreshType);
                model.SaveChanges();
            }, cancellationToken);

            return (true, $"Refresh completed for partition '{partitionName}'.");
        }
        catch (Exception ex)
        {
            return (false, $"Refresh failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RefreshModelAsync(
        ModelRefreshType refreshType, CancellationToken cancellationToken = default)
    {
        var model = _connectionService.GetTomModel();
        if (model == null)
            return (false, "No model connected.");

        try
        {
            var tomRefreshType = MapRefreshType(refreshType);

            await Task.Run(() =>
            {
                model.RequestRefresh(tomRefreshType);
                model.SaveChanges();
            }, cancellationToken);

            return (true, "Model refresh completed.");
        }
        catch (Exception ex)
        {
            return (false, $"Model refresh failed: {ex.Message}");
        }
    }

    public CancellationTokenSource CreateCancellationSource()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        return _cancellationTokenSource;
    }

    public void CancelCurrentRefresh()
    {
        _cancellationTokenSource?.Cancel();
    }

    private static TomRefreshType MapRefreshType(ModelRefreshType refreshType)
    {
        return refreshType switch
        {
            ModelRefreshType.Full => TomRefreshType.Full,
            ModelRefreshType.Calculate => TomRefreshType.Calculate,
            ModelRefreshType.ClearValues => TomRefreshType.ClearValues,
            ModelRefreshType.DataOnly => TomRefreshType.DataOnly,
            ModelRefreshType.Defragment => TomRefreshType.Defragment,
            _ => TomRefreshType.Full
        };
    }
}
