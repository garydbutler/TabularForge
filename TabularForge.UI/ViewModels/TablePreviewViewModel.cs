using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class TablePreviewViewModel : ObservableObject
{
    private readonly QueryService _queryService;
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private DataTable? _previewData;

    [ObservableProperty]
    private string _statusText = "Select a table to preview data.";

    [ObservableProperty]
    private string _rowCountText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _previewRowCount = 1000;

    [ObservableProperty]
    private ObservableCollection<string> _availableTables = new();

    public event EventHandler<string>? MessageLogged;

    public TablePreviewViewModel(QueryService queryService, ConnectionService connectionService)
    {
        _queryService = queryService;
        _connectionService = connectionService;
    }

    partial void OnTableNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            _ = LoadPreviewAsync();
    }

    [RelayCommand]
    private async Task LoadPreview()
    {
        await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        if (string.IsNullOrEmpty(TableName)) return;
        if (!_connectionService.IsConnected)
        {
            ErrorMessage = "Not connected to a server.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusText = $"Loading {TableName}...";

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await _queryService.PreviewTableAsync(TableName, PreviewRowCount, _cancellationTokenSource.Token);
            if (result.IsSuccess)
            {
                PreviewData = result.Data;
                RowCountText = $"{result.RowCount:N0} row(s) loaded";
                StatusText = $"{TableName}: {result.RowCount:N0} rows (preview limited to {PreviewRowCount:N0})";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                StatusText = "Error loading preview";
            }

            // Also get total count
            var countResult = await _queryService.GetTableRowCountAsync(TableName, _cancellationTokenSource.Token);
            if (countResult.IsSuccess && countResult.Data.Rows.Count > 0)
            {
                var totalRows = countResult.Data.Rows[0][0];
                RowCountText = $"{result.RowCount:N0} of {totalRows:N0} total row(s)";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Preview cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Error loading preview";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExportToCsv()
    {
        if (PreviewData == null || PreviewData.Rows.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Table Data to CSV",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"{TableName}_preview.csv"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        var headers = new List<string>();
        foreach (DataColumn col in PreviewData.Columns)
            headers.Add(EscapeCsvField(col.ColumnName));
        sb.AppendLine(string.Join(",", headers));

        foreach (DataRow row in PreviewData.Rows)
        {
            var fields = new List<string>();
            foreach (DataColumn col in PreviewData.Columns)
                fields.Add(EscapeCsvField(row[col]?.ToString() ?? ""));
            sb.AppendLine(string.Join(",", fields));
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageLogged?.Invoke(this, $"Table data exported to {dlg.FileName}");
    }

    public void RefreshTableList(IEnumerable<string> tableNames)
    {
        AvailableTables.Clear();
        foreach (var name in tableNames)
            AvailableTables.Add(name);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
