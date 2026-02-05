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

        // Short expression heuristic: if the expression is a simple single-line expression
        // (no existing newlines, under MaxLineLength), just normalize spaces and casing.
        if (IsShortSingleLineExpression(daxExpression, tokens))
        {
            return FormatInline(tokens);
        }

        return FormatMultiLine(tokens);
    }

    /// <summary>
    /// Determines if the expression is short enough to stay on a single line.
    /// An expression is "short single-line" if:
    /// - It has no existing newline characters
    /// - It contains no top-level structural keywords (EVALUATE, DEFINE, VAR, RETURN)
    /// - Its trimmed length is under MaxLineLength
    /// </summary>
    private bool IsShortSingleLineExpression(string expression, List<DaxToken> tokens)
    {
        var trimmed = expression.Trim();

        // Contains newlines - treat as multi-line
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
            return false;

        // Too long for single line
        if (trimmed.Length > _options.MaxLineLength)
            return false;

        // Contains structural keywords that need multi-line formatting
        foreach (var t in tokens)
        {
            if (t.Type is DaxTokenType.Evaluate or DaxTokenType.Define
                or DaxTokenType.Var or DaxTokenType.Return
                or DaxTokenType.Measure or DaxTokenType.Column)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Format as a single line: normalize whitespace, uppercase functions/keywords.
    /// </summary>
    private string FormatInline(List<DaxToken> tokens)
    {
        var sb = new StringBuilder();
        DaxToken? prev = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            switch (token.Type)
            {
                case DaxTokenType.EOF:
                case DaxTokenType.Newline:
                    break;

                case DaxTokenType.Whitespace:
                    // Normalize to single space, but don't add leading space
                    if (sb.Length > 0 && prev != null && !prev.IsTrivia)
                        sb.Append(' ');
                    break;

                case DaxTokenType.SingleLineComment:
                case DaxTokenType.MultiLineComment:
                    if (_options.PreserveComments)
                    {
                        if (sb.Length > 0 && prev != null && prev.Type != DaxTokenType.Whitespace)
                            sb.Append(' ');
                        sb.Append(token.Text);
                    }
                    break;

                case DaxTokenType.Comma:
                    sb.Append(',');
                    if (_options.SpaceAfterComma)
                        sb.Append(' ');
                    break;

                case DaxTokenType.OpenParen:
                    sb.Append('(');
                    break;

                case DaxTokenType.CloseParen:
                    sb.Append(')');
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
                    if (_options.SpaceAroundOperators)
                    {
                        // Remove trailing space if we're about to add one
                        if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                            sb.Append(' ');
                        sb.Append(token.Text);
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append(token.Text);
                    }
                    break;

                case DaxTokenType.Identifier:
                    if (_options.UppercaseFunctions && DaxFunctionCatalog.TryGetFunction(token.Text, out _))
                        sb.Append(token.Text.ToUpperInvariant());
                    else
                        sb.Append(token.Text);
                    break;

                default:
                    if (token.IsKeyword)
                        sb.Append(FormatKeyword(token.Text));
                    else
                        sb.Append(token.Text);
                    break;
            }

            if (!token.IsTrivia)
                prev = token;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Full multi-line formatting with indentation and line breaks.
    /// </summary>
    private string FormatMultiLine(List<DaxToken> tokens)
    {
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
                    AppendIndent(sb, 0);
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
                    AppendIndent(sb, 1);
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
                    // Only break after open paren if args would exceed max line length
                    if (_options.AlignFunctionParameters && prev != null &&
                        (prev.Type == DaxTokenType.Identifier || prev.IsKeyword) &&
                        ShouldBreakFunctionArgs(tokens, i, _options.MaxLineLength, indentLevel))
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
                    // Only break after comma if the function args are long enough to warrant it
                    if (_options.BreakAfterComma && IsInFunctionArgs(tokens, i) &&
                        ShouldBreakAtComma(tokens, i, _options.MaxLineLength, indentLevel))
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

    /// <summary>
    /// Determines whether to break function arguments across lines by measuring
    /// the total character length of the content between the parens.
    /// </summary>
    private static bool ShouldBreakFunctionArgs(List<DaxToken> tokens, int openParenIndex, int maxLineLength, int indentLevel)
    {
        int depth = 0;
        int charLength = 0;
        for (int i = openParenIndex; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == DaxTokenType.OpenParen) depth++;
            else if (t.Type == DaxTokenType.CloseParen)
            {
                depth--;
                if (depth == 0) break;
            }
            if (!t.IsTrivia)
                charLength += t.Text.Length + 1; // +1 for spacing
        }

        // Account for indentation and content before the paren
        int estimatedLineLength = (indentLevel * 4) + charLength;
        return estimatedLineLength > maxLineLength;
    }

    /// <summary>
    /// Determines whether to break at this comma by checking if the enclosing
    /// function call's arguments are long enough to need multi-line formatting.
    /// </summary>
    private static bool ShouldBreakAtComma(List<DaxToken> tokens, int commaIndex, int maxLineLength, int indentLevel)
    {
        // Find the enclosing open paren for this comma
        int depth = 0;
        for (int i = commaIndex - 1; i >= 0; i--)
        {
            if (tokens[i].Type == DaxTokenType.CloseParen) depth++;
            else if (tokens[i].Type == DaxTokenType.OpenParen)
            {
                if (depth == 0)
                {
                    // Found the enclosing open paren - measure the full arg span
                    return ShouldBreakFunctionArgs(tokens, i, maxLineLength, indentLevel);
                }
                depth--;
            }
        }
        return false;
    }

    private static bool IsInFunctionArgs(List<DaxToken> tokens, int commaIndex)
    {
        int depth = 0;
        for (int i = commaIndex - 1; i >= 0; i--)
        {
            if (tokens[i].Type == DaxTokenType.CloseParen) depth++;
            else if (tokens[i].Type == DaxTokenType.OpenParen)
            {
                if (depth == 0)
                {
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
