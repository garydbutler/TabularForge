using System.Text;
using TabularForge.DAXParser.Lexer;
using TabularForge.DAXParser.Semantics;

namespace TabularForge.DAXParser.Formatter;

public sealed class DaxFormatter
{
    private readonly DaxFormatterOptions _options;

    public DaxFormatter(DaxFormatterOptions? options = null)
    {
        _options = options ?? DaxFormatterOptions.Default;
    }

    public string Format(string daxExpression)
    {
        if (string.IsNullOrWhiteSpace(daxExpression))
            return daxExpression;

        var lexer = new DaxLexer(daxExpression);
        var tokens = lexer.Tokenize();

        var sb = new StringBuilder();
        int indentLevel = 0;
        bool atLineStart = true;
        bool lastWasNewline = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var prev = i > 0 ? tokens[i - 1] : null;
            var next = i + 1 < tokens.Count ? tokens[i + 1] : null;

            switch (token.Type)
            {
                case DaxTokenType.EOF:
                    break;

                case DaxTokenType.Newline:
                    if (!lastWasNewline)
                    {
                        sb.AppendLine();
                        atLineStart = true;
                        lastWasNewline = true;
                    }
                    break;

                case DaxTokenType.Whitespace:
                    if (!atLineStart && prev?.Type != DaxTokenType.Newline)
                    {
                        // Normalize whitespace to single space (except at line start)
                        sb.Append(' ');
                    }
                    break;

                case DaxTokenType.SingleLineComment:
                case DaxTokenType.MultiLineComment:
                    if (_options.PreserveComments)
                    {
                        if (atLineStart)
                        {
                            AppendIndent(sb, indentLevel);
                        }
                        sb.Append(token.Text);
                        atLineStart = false;
                        lastWasNewline = false;
                    }
                    break;

                case DaxTokenType.Evaluate:
                case DaxTokenType.Define:
                    if (!atLineStart && !lastWasNewline)
                    {
                        sb.AppendLine();
                    }
                    AppendIndent(sb, 0); // Top-level keywords at column 0
                    sb.Append(FormatKeyword(token.Text));
                    atLineStart = false;
                    lastWasNewline = false;
                    break;

                case DaxTokenType.Measure:
                case DaxTokenType.Column:
                    if (!atLineStart && !lastWasNewline)
                    {
                        sb.AppendLine();
                    }
                    AppendIndent(sb, 1); // Definition keywords indented once
                    sb.Append(FormatKeyword(token.Text));
                    atLineStart = false;
                    lastWasNewline = false;
                    break;

                case DaxTokenType.Var:
                    if (!atLineStart && !lastWasNewline)
                    {
                        sb.AppendLine();
                    }
                    AppendIndent(sb, indentLevel);
                    sb.Append(FormatKeyword(token.Text));
                    atLineStart = false;
                    lastWasNewline = false;
                    if (_options.IndentAfterKeywords)
                        indentLevel++;
                    break;

                case DaxTokenType.Return:
                    if (_options.IndentAfterKeywords && indentLevel > 0)
                        indentLevel--;
                    if (!atLineStart && !lastWasNewline)
                    {
                        sb.AppendLine();
                    }
                    AppendIndent(sb, indentLevel);
                    sb.Append(FormatKeyword(token.Text));
                    atLineStart = false;
                    lastWasNewline = false;
                    if (_options.BreakAfterReturn)
                    {
                        sb.AppendLine();
                        atLineStart = true;
                        lastWasNewline = true;
                        indentLevel++;
                    }
                    break;

                case DaxTokenType.OpenParen:
                    if (atLineStart) AppendIndent(sb, indentLevel);
                    sb.Append('(');
                    indentLevel++;
                    atLineStart = false;
                    lastWasNewline = false;
                    // Check if we should break after open paren for function calls
                    if (_options.AlignFunctionParameters && prev != null &&
                        (prev.Type == DaxTokenType.Identifier || prev.IsKeyword) &&
                        ShouldBreakFunctionArgs(tokens, i))
                    {
                        sb.AppendLine();
                        atLineStart = true;
                        lastWasNewline = true;
                    }
                    break;

                case DaxTokenType.CloseParen:
                    if (indentLevel > 0) indentLevel--;
                    if (atLineStart)
                    {
                        AppendIndent(sb, indentLevel);
                    }
                    sb.Append(')');
                    atLineStart = false;
                    lastWasNewline = false;
                    break;

                case DaxTokenType.Comma:
                    sb.Append(',');
                    atLineStart = false;
                    lastWasNewline = false;
                    if (_options.BreakAfterComma && IsInFunctionArgs(tokens, i))
                    {
                        sb.AppendLine();
                        atLineStart = true;
                        lastWasNewline = true;
                    }
                    else if (_options.SpaceAfterComma)
                    {
                        sb.Append(' ');
                    }
                    break;

                case DaxTokenType.Equals:
                case DaxTokenType.NotEquals:
                case DaxTokenType.LessThan:
                case DaxTokenType.GreaterThan:
                case DaxTokenType.LessEquals:
                case DaxTokenType.GreaterEquals:
                case DaxTokenType.DoubleAmpersand:
                case DaxTokenType.DoublePipe:
                case DaxTokenType.Plus:
                case DaxTokenType.Minus:
                case DaxTokenType.Star:
                case DaxTokenType.Slash:
                case DaxTokenType.Caret:
                case DaxTokenType.Ampersand:
                    if (atLineStart) AppendIndent(sb, indentLevel);
                    if (_options.SpaceAroundOperators && !atLineStart)
                        sb.Append(' ');
                    sb.Append(token.Text);
                    if (_options.SpaceAroundOperators)
                        sb.Append(' ');
                    atLineStart = false;
                    lastWasNewline = false;
                    break;

                case DaxTokenType.Identifier:
                    if (atLineStart) AppendIndent(sb, indentLevel);
                    sb.Append(_options.UppercaseFunctions && DaxFunctionCatalog.TryGetFunction(token.Text, out _)
                        ? token.Text.ToUpperInvariant()
                        : token.Text);
                    atLineStart = false;
                    lastWasNewline = false;
                    break;

                default:
                    // Keywords we haven't handled specifically
                    if (token.IsKeyword)
                    {
                        if (atLineStart) AppendIndent(sb, indentLevel);
                        sb.Append(FormatKeyword(token.Text));
                    }
                    else
                    {
                        if (atLineStart) AppendIndent(sb, indentLevel);
                        sb.Append(token.Text);
                    }
                    atLineStart = false;
                    lastWasNewline = false;
                    break;
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string FormatKeyword(string keyword)
    {
        return _options.UppercaseKeywords ? keyword.ToUpperInvariant() : keyword;
    }

    private void AppendIndent(StringBuilder sb, int level)
    {
        for (int i = 0; i < level; i++)
            sb.Append(_options.IndentString);
    }

    private static bool ShouldBreakFunctionArgs(List<DaxToken> tokens, int openParenIndex)
    {
        // Count non-trivia tokens until matching close paren
        int depth = 0;
        int tokenCount = 0;
        for (int i = openParenIndex; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == DaxTokenType.OpenParen) depth++;
            else if (t.Type == DaxTokenType.CloseParen)
            {
                depth--;
                if (depth == 0) break;
            }
            if (!t.IsTrivia) tokenCount++;
        }
        return tokenCount > 6; // Break if more than a few tokens in args
    }

    private static bool IsInFunctionArgs(List<DaxToken> tokens, int commaIndex)
    {
        // Walk backwards to find if we're inside a function call's parentheses
        int depth = 0;
        for (int i = commaIndex - 1; i >= 0; i--)
        {
            if (tokens[i].Type == DaxTokenType.CloseParen) depth++;
            else if (tokens[i].Type == DaxTokenType.OpenParen)
            {
                if (depth == 0)
                {
                    // Check if preceded by an identifier (function name)
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (tokens[j].IsTrivia) continue;
                        return tokens[j].Type == DaxTokenType.Identifier || tokens[j].IsKeyword;
                    }
                    return false;
                }
                depth--;
            }
        }
        return false;
    }
}
