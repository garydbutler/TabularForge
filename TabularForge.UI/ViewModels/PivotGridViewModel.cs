using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class PivotGridViewModel : ObservableObject
{
    private readonly QueryService _queryService;
    private readonly ConnectionService _connectionService;

    // === Available Fields (from model) ===

    [ObservableProperty]
    private ObservableCollection<PivotField> _availableFields = new();

    // === Area Fields ===

    [ObservableProperty]
    private ObservableCollection<PivotField> _rowFields = new();

    [ObservableProperty]
    private ObservableCollection<PivotField> _columnFields = new();

    [ObservableProperty]
    private ObservableCollection<PivotField> _valueFields = new();

    [ObservableProperty]
    private ObservableCollection<PivotField> _filterFields = new();

    // === Results ===

    [ObservableProperty]
    private DataTable? _pivotData;

    [ObservableProperty]
    private string _statusText = "Drag fields to the areas below to build a pivot table.";

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _rowCountText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string? _errorMessage;

    // === Options ===

    [ObservableProperty]
    private bool _showGrandTotals = true;

    [ObservableProperty]
    private bool _showSubTotals = true;

    [ObservableProperty]
    private bool _isModelLoaded;

    // === Selected field for drag ===

    [ObservableProperty]
    private PivotField? _selectedAvailableField;

    public event EventHandler<string>? MessageLogged;

    private CancellationTokenSource? _cts;

    public PivotGridViewModel(QueryService queryService, ConnectionService connectionService)
    {
        _queryService = queryService;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Load available fields from the TOM model tree.
    /// </summary>
    public void LoadFieldsFromModel(TomNode? modelRoot)
    {
        AvailableFields.Clear();
        PivotData = null;

        if (modelRoot == null)
        {
            IsModelLoaded = false;
            StatusText = "No model loaded";
            return;
        }

        CollectFields(modelRoot);
        IsModelLoaded = true;
        StatusText = $"{AvailableFields.Count} fields available. Drag fields to areas to build pivot.";
    }

    private void CollectFields(TomNode node)
    {
        if (node.ObjectType == TomObjectType.Table)
        {
            CollectTableFields(node, node.Name);
        }

        foreach (var child in node.Children)
            CollectFields(child);
    }

    private void CollectTableFields(TomNode node, string tableName)
    {
        switch (node.ObjectType)
        {
            case TomObjectType.DataColumn:
            case TomObjectType.Column:
            case TomObjectType.CalculatedColumn:
            case TomObjectType.CalculatedTableColumn:
                AvailableFields.Add(new PivotField
                {
                    FieldName = node.Name,
                    TableName = tableName,
                    IsMeasure = false
                });
                break;

            case TomObjectType.Measure:
                AvailableFields.Add(new PivotField
                {
                    FieldName = node.Name,
                    TableName = tableName,
                    IsMeasure = true
                });
                break;
        }

        foreach (var child in node.Children)
            CollectTableFields(child, tableName);
    }

    // === Area Management ===

    [RelayCommand]
    private void AddToRows(PivotField? field)
    {
        if (field == null) return;
        RemoveFromAllAreas(field);
        field.Area = PivotAreaType.Rows;
        RowFields.Add(field);
    }

    [RelayCommand]
    private void AddToColumns(PivotField? field)
    {
        if (field == null) return;
        RemoveFromAllAreas(field);
        field.Area = PivotAreaType.Columns;
        ColumnFields.Add(field);
    }

    [RelayCommand]
    private void AddToValues(PivotField? field)
    {
        if (field == null) return;
        RemoveFromAllAreas(field);
        field.Area = PivotAreaType.Values;
        ValueFields.Add(field);
    }

    [RelayCommand]
    private void AddToFilters(PivotField? field)
    {
        if (field == null) return;
        RemoveFromAllAreas(field);
        field.Area = PivotAreaType.Filters;
        FilterFields.Add(field);
    }

    [RelayCommand]
    private void RemoveField(PivotField? field)
    {
        if (field == null) return;
        RemoveFromAllAreas(field);
    }

    private void RemoveFromAllAreas(PivotField field)
    {
        RowFields.Remove(field);
        ColumnFields.Remove(field);
        ValueFields.Remove(field);
        FilterFields.Remove(field);
    }

    [RelayCommand]
    private void ClearAllAreas()
    {
        RowFields.Clear();
        ColumnFields.Clear();
        ValueFields.Clear();
        FilterFields.Clear();
        PivotData = null;
        StatusText = "All areas cleared.";
    }

    // === Execute Pivot Query ===

    [RelayCommand]
    private async Task ExecutePivot()
    {
        if (!_connectionService.IsConnected)
        {
            ErrorMessage = "Not connected to a server.";
            StatusText = "Error: Not connected";
            return;
        }

        if (ValueFields.Count == 0)
        {
            ErrorMessage = "Add at least one field to the Values area.";
            return;
        }

        IsExecuting = true;
        ErrorMessage = null;
        StatusText = "Executing pivot query...";
        PivotData = null;

        _cts = new CancellationTokenSource();

        try
        {
            var daxQuery = BuildPivotDaxQuery();
            MessageLogged?.Invoke(this, $"Pivot DAX: {daxQuery}");

            var result = await _queryService.ExecuteQueryAsync(daxQuery, _cts.Token);

            if (result.IsSuccess)
            {
                PivotData = result.Data;
                RowCountText = $"{result.RowCount:N0} rows";
                ExecutionTimeText = FormatDuration(result.ExecutionTime);
                StatusText = $"Pivot complete: {result.RowCount:N0} rows in {FormatDuration(result.ExecutionTime)}";
                MessageLogged?.Invoke(this, $"Pivot executed: {result.RowCount} rows");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                StatusText = "Pivot query failed";
                MessageLogged?.Invoke(this, $"Pivot error: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Pivot query failed";
        }
        finally
        {
            IsExecuting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelPivot()
    {
        _cts?.Cancel();
        StatusText = "Cancelling...";
    }

    /// <summary>
    /// Build a SUMMARIZECOLUMNS DAX query from the pivot field configuration.
    /// </summary>
    private string BuildPivotDaxQuery()
    {
        var sb = new StringBuilder();
        sb.AppendLine("EVALUATE");

        // Use SUMMARIZECOLUMNS for grouping
        var groupByCols = new List<string>();
        foreach (var f in RowFields)
            groupByCols.Add(f.FullName);
        foreach (var f in ColumnFields)
            groupByCols.Add(f.FullName);

        if (groupByCols.Count > 0)
        {
            sb.Append("SUMMARIZECOLUMNS(");
            sb.AppendLine();

            // Group by columns
            for (int i = 0; i < groupByCols.Count; i++)
            {
                sb.Append($"    {groupByCols[i]}");
                if (i < groupByCols.Count - 1 || ValueFields.Any(f => f.IsMeasure))
                    sb.Append(",");
                sb.AppendLine();
            }

            // Filter columns
            foreach (var f in FilterFields)
            {
                sb.AppendLine($"    FILTER(ALL({f.FullName}), {f.FullName} <> BLANK()),");
            }

            // Value measures
            var measures = ValueFields.Where(f => f.IsMeasure).ToList();
            for (int i = 0; i < measures.Count; i++)
            {
                sb.Append($"    \"{measures[i].FieldName}\", {measures[i].FullName}");
                if (i < measures.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            // Value columns (non-measures, wrap in COUNTROWS or SUM)
            var columns = ValueFields.Where(f => !f.IsMeasure).ToList();
            if (columns.Count > 0 && measures.Count > 0)
                sb.Append(",");
            for (int i = 0; i < columns.Count; i++)
            {
                var agg = columns[i].Aggregation switch
                {
                    "Count" => $"COUNTROWS('{columns[i].TableName}')",
                    "Sum" => $"SUM({columns[i].FullName})",
                    "Average" => $"AVERAGE({columns[i].FullName})",
                    "Min" => $"MIN({columns[i].FullName})",
                    "Max" => $"MAX({columns[i].FullName})",
                    _ => $"SUM({columns[i].FullName})"
                };
                sb.Append($"    \"{columns[i].FieldName}_{columns[i].Aggregation}\", {agg}");
                if (i < columns.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine(")");
        }
        else
        {
            // No group by, just evaluate measures in a ROW
            sb.Append("ROW(");
            var fields = ValueFields.ToList();
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].IsMeasure)
                    sb.Append($"\"{fields[i].FieldName}\", {fields[i].FullName}");
                else
                    sb.Append($"\"{fields[i].FieldName}\", COUNTROWS('{fields[i].TableName}')");
                if (i < fields.Count - 1) sb.Append(", ");
            }
            sb.AppendLine(")");
        }

        return sb.ToString();
    }

    // === Export ===

    [RelayCommand]
    private void ExportToCsv()
    {
        if (PivotData == null || PivotData.Rows.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Pivot Results to CSV",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();

        // Header
        var headers = new List<string>();
        foreach (DataColumn col in PivotData.Columns)
            headers.Add(EscapeCsv(col.ColumnName));
        sb.AppendLine(string.Join(",", headers));

        // Rows
        foreach (DataRow row in PivotData.Rows)
        {
            var fields = new List<string>();
            foreach (DataColumn col in PivotData.Columns)
                fields.Add(EscapeCsv(row[col]?.ToString() ?? ""));
            sb.AppendLine(string.Join(",", fields));
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageLogged?.Invoke(this, $"Pivot exported to {dlg.FileName}");
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        // Basic tab-separated export that Excel can open
        if (PivotData == null || PivotData.Rows.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Pivot Results",
            Filter = "Tab-Separated (*.txt)|*.txt|CSV (*.csv)|*.csv",
            DefaultExt = ".txt"
        };

        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        var sep = dlg.FileName.EndsWith(".csv") ? "," : "\t";

        var headers = new List<string>();
        foreach (DataColumn col in PivotData.Columns)
            headers.Add(col.ColumnName);
        sb.AppendLine(string.Join(sep, headers));

        foreach (DataRow row in PivotData.Rows)
        {
            var fields = new List<string>();
            foreach (DataColumn col in PivotData.Columns)
                fields.Add(row[col]?.ToString() ?? "");
            sb.AppendLine(string.Join(sep, fields));
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageLogged?.Invoke(this, $"Pivot exported to {dlg.FileName}");
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalMilliseconds < 1000) return $"{d.TotalMilliseconds:F0} ms";
        if (d.TotalSeconds < 60) return $"{d.TotalSeconds:F2} sec";
        return $"{d.TotalMinutes:F1} min";
    }
}
