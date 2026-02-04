namespace TabularForge.DAXParser.Formatter;

public sealed class DaxFormatterOptions
{
    public bool UseTabs { get; set; } = false;
    public int IndentSize { get; set; } = 4;
    public int MaxLineLength { get; set; } = 120;
    public bool IndentAfterKeywords { get; set; } = true;
    public bool AlignFunctionParameters { get; set; } = true;
    public bool BreakBeforeComma { get; set; } = false;
    public bool BreakAfterComma { get; set; } = true;
    public bool SpaceAfterComma { get; set; } = true;
    public bool SpaceAroundOperators { get; set; } = true;
    public bool UppercaseKeywords { get; set; } = true;
    public bool UppercaseFunctions { get; set; } = true;
    public bool PreserveComments { get; set; } = true;
    public bool BreakAfterReturn { get; set; } = true;
    public bool NewLineAfterVar { get; set; } = true;
    public bool CompactShortExpressions { get; set; } = true;
    public int CompactThreshold { get; set; } = 60;

    public string IndentString => UseTabs ? "\t" : new string(' ', IndentSize);

    public static DaxFormatterOptions Default => new();

    public static DaxFormatterOptions Compact => new()
    {
        MaxLineLength = 200,
        AlignFunctionParameters = false,
        BreakAfterComma = false,
        CompactShortExpressions = true,
        CompactThreshold = 120
    };
}
