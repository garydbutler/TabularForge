namespace TabularForge.Core.Models;

public enum RefreshType
{
    Full,
    Calculate,
    ClearValues,
    DataOnly,
    Defragment
}

public enum RefreshState
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public class RefreshOperation
{
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public RefreshType RefreshType { get; set; } = RefreshType.Full;
    public RefreshState State { get; set; } = RefreshState.Queued;
    public double ProgressPercent { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
}

public class RefreshHistoryEntry
{
    public string ObjectName { get; set; } = string.Empty;
    public RefreshType RefreshType { get; set; }
    public RefreshState FinalState { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public string? ErrorMessage { get; set; }
}
