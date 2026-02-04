using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

public enum PivotAreaType
{
    Rows,
    Columns,
    Values,
    Filters
}

public partial class PivotField : ObservableObject
{
    [ObservableProperty]
    private string _fieldName = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private PivotAreaType _area;

    [ObservableProperty]
    private bool _isMeasure;

    [ObservableProperty]
    private string _formatString = string.Empty;

    [ObservableProperty]
    private string _aggregation = "Sum";

    public string DisplayName => string.IsNullOrEmpty(TableName)
        ? FieldName
        : $"{TableName}[{FieldName}]";

    public string FullName => $"'{TableName}'[{FieldName}]";
}

public class PivotFilterValue
{
    public string Value { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}

public class PivotCellValue
{
    public int Row { get; set; }
    public int Column { get; set; }
    public object? Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
}

public class PivotResult
{
    public DataTable Data { get; set; } = new();
    public List<string> RowHeaders { get; set; } = new();
    public List<string> ColumnHeaders { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => ErrorMessage == null;
    public TimeSpan ExecutionTime { get; set; }
}
