using System.Data;

namespace TabularForge.Core.Models;

public class QueryResult
{
    public DataTable Data { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public long RowCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => ErrorMessage == null;
}
