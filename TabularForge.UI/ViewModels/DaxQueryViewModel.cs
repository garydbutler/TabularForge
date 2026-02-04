using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class DaxQueryViewModel : ObservableObject
{
    private readonly QueryService _queryService;
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _queryText = "EVALUATE\n\n";

    [ObservableProperty]
    private DataTable? _resultData;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _rowCountText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<DaxQueryTabItem> _queryTabs = new();

    [ObservableProperty]
    private DaxQueryTabItem? _activeTab;

    public event EventHandler<string>? MessageLogged;

    public DaxQueryViewModel(QueryService queryService, ConnectionService connectionService)
    {
        _queryService = queryService;
        _connectionService = connectionService;

        // Create default first tab
        var tab = new DaxQueryTabItem { Title = "Query 1", QueryText = "EVALUATE\n\n" };
        QueryTabs.Add(tab);
        ActiveTab = tab;
    }

    partial void OnActiveTabChanged(DaxQueryTabItem? value)
    {
        if (value != null)
            QueryText = value.QueryText;
    }

    [RelayCommand]
    private async Task ExecuteQuery()
    {
        if (!_connectionService.IsConnected)
        {
            ErrorMessage = "Not connected to a server. Please connect first.";
            StatusText = "Error: Not connected";
            return;
        }

        if (string.IsNullOrWhiteSpace(QueryText))
        {
            ErrorMessage = "Please enter a DAX query.";
            return;
        }

        IsExecuting = true;
        ErrorMessage = null;
        StatusText = "Executing query...";
        ResultData = null;

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await _queryService.ExecuteQueryAsync(QueryText, _cancellationTokenSource.Token);

            if (result.IsSuccess)
            {
                ResultData = result.Data;
                RowCountText = $"{result.RowCount:N0} row(s)";
                ExecutionTimeText = FormatDuration(result.ExecutionTime);
                StatusText = $"Query completed: {result.RowCount:N0} rows in {FormatDuration(result.ExecutionTime)}";
                MessageLogged?.Invoke(this, $"Query executed: {result.RowCount} rows in {FormatDuration(result.ExecutionTime)}");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                StatusText = "Query failed";
                MessageLogged?.Invoke(this, $"Query error: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Query failed";
        }
        finally
        {
            IsExecuting = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelQuery()
    {
        _cancellationTokenSource?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private void NewQueryTab()
    {
        var tabNumber = QueryTabs.Count + 1;
        var tab = new DaxQueryTabItem
        {
            Title = $"Query {tabNumber}",
            QueryText = "EVALUATE\n\n"
        };
        QueryTabs.Add(tab);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void CloseQueryTab(DaxQueryTabItem? tab)
    {
        if (tab == null || QueryTabs.Count <= 1) return;
        var index = QueryTabs.IndexOf(tab);
        QueryTabs.Remove(tab);
        ActiveTab = QueryTabs[Math.Max(0, index - 1)];
    }

    [RelayCommand]
    private void ExportToCsv()
    {
        if (ResultData == null || ResultData.Rows.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Results to CSV",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();

        // Header
        var headers = new List<string>();
        foreach (DataColumn col in ResultData.Columns)
            headers.Add(EscapeCsvField(col.ColumnName));
        sb.AppendLine(string.Join(",", headers));

        // Rows
        foreach (DataRow row in ResultData.Rows)
        {
            var fields = new List<string>();
            foreach (DataColumn col in ResultData.Columns)
                fields.Add(EscapeCsvField(row[col]?.ToString() ?? ""));
            sb.AppendLine(string.Join(",", fields));
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageLogged?.Invoke(this, $"Results exported to {dlg.FileName}");
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (ResultData == null || ResultData.Rows.Count == 0) return;

        var sb = new StringBuilder();

        // Header
        var headers = new List<string>();
        foreach (DataColumn col in ResultData.Columns)
            headers.Add(col.ColumnName);
        sb.AppendLine(string.Join("\t", headers));

        // Rows
        foreach (DataRow row in ResultData.Rows)
        {
            var fields = new List<string>();
            foreach (DataColumn col in ResultData.Columns)
                fields.Add(row[col]?.ToString() ?? "");
            sb.AppendLine(string.Join("\t", fields));
        }

        System.Windows.Clipboard.SetText(sb.ToString());
        MessageLogged?.Invoke(this, "Results copied to clipboard.");
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
            return $"{duration.TotalMilliseconds:F0} ms";
        if (duration.TotalSeconds < 60)
            return $"{duration.TotalSeconds:F2} sec";
        return $"{duration.TotalMinutes:F1} min";
    }
}

public partial class DaxQueryTabItem : ObservableObject
{
    [ObservableProperty]
    private string _title = "Query";

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private bool _isModified;
}
