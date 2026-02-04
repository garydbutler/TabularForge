namespace TabularForge.DAXParser.Semantics;

public enum DaxDiagnosticSeverity
{
    Error,
    Warning,
    Info
}

public sealed class DaxDiagnostic
{
    public DaxDiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int StartOffset { get; set; }
    public int Length { get; set; }
    public string? Source { get; set; }

    public DaxDiagnostic() { }

    public DaxDiagnostic(DaxDiagnosticSeverity severity, string message, int line, int column, int startOffset = 0, int length = 0)
    {
        Severity = severity;
        Message = message;
        Line = line;
        Column = column;
        StartOffset = startOffset;
        Length = length;
    }

    public override string ToString() => $"{Severity} ({Line},{Column}): {Message}";
}
