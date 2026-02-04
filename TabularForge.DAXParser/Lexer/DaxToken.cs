namespace TabularForge.DAXParser.Lexer;

public sealed class DaxToken
{
    public DaxTokenType Type { get; }
    public string Text { get; }
    public int StartOffset { get; }
    public int EndOffset => StartOffset + Text.Length;
    public int Line { get; }
    public int Column { get; }

    public DaxToken(DaxTokenType type, string text, int startOffset, int line, int column)
    {
        Type = type;
        Text = text;
        StartOffset = startOffset;
        Line = line;
        Column = column;
    }

    public bool IsTrivia => Type is DaxTokenType.Whitespace or DaxTokenType.Newline
        or DaxTokenType.SingleLineComment or DaxTokenType.MultiLineComment;

    public bool IsKeyword => Type is >= DaxTokenType.Evaluate and <= DaxTokenType.Table;

    public override string ToString() => $"{Type}({Text})@{Line}:{Column}";
}
