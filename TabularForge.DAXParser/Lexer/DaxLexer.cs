namespace TabularForge.DAXParser.Lexer;

public sealed class DaxLexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    private static readonly Dictionary<string, DaxTokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EVALUATE"] = DaxTokenType.Evaluate,
        ["DEFINE"] = DaxTokenType.Define,
        ["MEASURE"] = DaxTokenType.Measure,
        ["COLUMN"] = DaxTokenType.Column,
        ["VAR"] = DaxTokenType.Var,
        ["RETURN"] = DaxTokenType.Return,
        ["ORDER"] = DaxTokenType.Order,
        ["BY"] = DaxTokenType.By,
        ["ASC"] = DaxTokenType.Asc,
        ["DESC"] = DaxTokenType.Desc,
        ["START"] = DaxTokenType.Start,
        ["AT"] = DaxTokenType.At,
        ["IN"] = DaxTokenType.In,
        ["NOT"] = DaxTokenType.Not,
        ["AND"] = DaxTokenType.And,
        ["OR"] = DaxTokenType.Or,
        ["TRUE"] = DaxTokenType.True,
        ["FALSE"] = DaxTokenType.False,
        ["BLANK"] = DaxTokenType.Blank,
        ["TABLE"] = DaxTokenType.Table,
    };

    public DaxLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public List<DaxToken> Tokenize()
    {
        var tokens = new List<DaxToken>();
        while (_pos < _source.Length)
        {
            var token = NextToken();
            tokens.Add(token);
        }
        tokens.Add(new DaxToken(DaxTokenType.EOF, string.Empty, _pos, _line, _col));
        return tokens;
    }

    private DaxToken NextToken()
    {
        var startPos = _pos;
        var startLine = _line;
        var startCol = _col;
        var c = _source[_pos];

        // Newlines
        if (c == '\r' || c == '\n')
        {
            return ReadNewline(startPos, startLine, startCol);
        }

        // Whitespace
        if (char.IsWhiteSpace(c))
        {
            return ReadWhitespace(startPos, startLine, startCol);
        }

        // Comments
        if (c == '/' && _pos + 1 < _source.Length)
        {
            if (_source[_pos + 1] == '/')
                return ReadSingleLineComment(startPos, startLine, startCol);
            if (_source[_pos + 1] == '*')
                return ReadMultiLineComment(startPos, startLine, startCol);
        }

        // Line comments with --
        if (c == '-' && _pos + 1 < _source.Length && _source[_pos + 1] == '-')
        {
            return ReadSingleLineComment(startPos, startLine, startCol);
        }

        // String literals
        if (c == '"')
        {
            return ReadString(startPos, startLine, startCol);
        }

        // Table references
        if (c == '\'')
        {
            return ReadTableReference(startPos, startLine, startCol);
        }

        // Column references
        if (c == '[')
        {
            return ReadColumnReference(startPos, startLine, startCol);
        }

        // Numbers
        if (char.IsDigit(c) || (c == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
        {
            return ReadNumber(startPos, startLine, startCol);
        }

        // Identifiers and keywords
        if (char.IsLetter(c) || c == '_')
        {
            return ReadIdentifierOrKeyword(startPos, startLine, startCol);
        }

        // Operators and delimiters
        return ReadOperatorOrDelimiter(startPos, startLine, startCol);
    }

    private DaxToken ReadNewline(int startPos, int startLine, int startCol)
    {
        if (_source[_pos] == '\r' && _pos + 1 < _source.Length && _source[_pos + 1] == '\n')
        {
            _pos += 2;
            _line++;
            _col = 1;
            return new DaxToken(DaxTokenType.Newline, "\r\n", startPos, startLine, startCol);
        }
        _pos++;
        _line++;
        _col = 1;
        return new DaxToken(DaxTokenType.Newline, _source[startPos].ToString(), startPos, startLine, startCol);
    }

    private DaxToken ReadWhitespace(int startPos, int startLine, int startCol)
    {
        while (_pos < _source.Length && _source[_pos] != '\r' && _source[_pos] != '\n'
               && char.IsWhiteSpace(_source[_pos]))
        {
            _pos++;
            _col++;
        }
        return new DaxToken(DaxTokenType.Whitespace, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadSingleLineComment(int startPos, int startLine, int startCol)
    {
        while (_pos < _source.Length && _source[_pos] != '\r' && _source[_pos] != '\n')
        {
            _pos++;
            _col++;
        }
        return new DaxToken(DaxTokenType.SingleLineComment, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadMultiLineComment(int startPos, int startLine, int startCol)
    {
        _pos += 2;
        _col += 2;
        while (_pos < _source.Length)
        {
            if (_source[_pos] == '*' && _pos + 1 < _source.Length && _source[_pos + 1] == '/')
            {
                _pos += 2;
                _col += 2;
                break;
            }
            if (_source[_pos] == '\n')
            {
                _line++;
                _col = 1;
            }
            else if (_source[_pos] == '\r')
            {
                // handled with \n
            }
            else
            {
                _col++;
            }
            _pos++;
        }
        return new DaxToken(DaxTokenType.MultiLineComment, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadString(int startPos, int startLine, int startCol)
    {
        _pos++; // skip opening "
        _col++;
        while (_pos < _source.Length)
        {
            if (_source[_pos] == '"')
            {
                _pos++;
                _col++;
                // Check for escaped quote ""
                if (_pos < _source.Length && _source[_pos] == '"')
                {
                    _pos++;
                    _col++;
                    continue;
                }
                break;
            }
            if (_source[_pos] == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
            _pos++;
        }
        return new DaxToken(DaxTokenType.String, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadTableReference(int startPos, int startLine, int startCol)
    {
        _pos++; // skip opening '
        _col++;
        while (_pos < _source.Length && _source[_pos] != '\'')
        {
            _col++;
            _pos++;
        }
        if (_pos < _source.Length)
        {
            _pos++; // skip closing '
            _col++;
        }
        return new DaxToken(DaxTokenType.TableReference, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadColumnReference(int startPos, int startLine, int startCol)
    {
        _pos++; // skip opening [
        _col++;
        while (_pos < _source.Length && _source[_pos] != ']')
        {
            _col++;
            _pos++;
        }
        if (_pos < _source.Length)
        {
            _pos++; // skip closing ]
            _col++;
        }
        return new DaxToken(DaxTokenType.ColumnReference, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadNumber(int startPos, int startLine, int startCol)
    {
        while (_pos < _source.Length && char.IsDigit(_source[_pos]))
        {
            _pos++;
            _col++;
        }
        if (_pos < _source.Length && _source[_pos] == '.')
        {
            _pos++;
            _col++;
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
            {
                _pos++;
                _col++;
            }
        }
        // Scientific notation
        if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
        {
            _pos++;
            _col++;
            if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
            {
                _pos++;
                _col++;
            }
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
            {
                _pos++;
                _col++;
            }
        }
        return new DaxToken(DaxTokenType.Number, _source[startPos.._pos], startPos, startLine, startCol);
    }

    private DaxToken ReadIdentifierOrKeyword(int startPos, int startLine, int startCol)
    {
        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_' || _source[_pos] == '.'))
        {
            _pos++;
            _col++;
        }

        var text = _source[startPos.._pos];
        if (Keywords.TryGetValue(text, out var keywordType))
        {
            return new DaxToken(keywordType, text, startPos, startLine, startCol);
        }
        return new DaxToken(DaxTokenType.Identifier, text, startPos, startLine, startCol);
    }

    private DaxToken ReadOperatorOrDelimiter(int startPos, int startLine, int startCol)
    {
        var c = _source[_pos];
        _pos++;
        _col++;

        switch (c)
        {
            case '(':
                return new DaxToken(DaxTokenType.OpenParen, "(", startPos, startLine, startCol);
            case ')':
                return new DaxToken(DaxTokenType.CloseParen, ")", startPos, startLine, startCol);
            case ']':
                return new DaxToken(DaxTokenType.CloseBracket, "]", startPos, startLine, startCol);
            case '{':
                return new DaxToken(DaxTokenType.OpenBrace, "{", startPos, startLine, startCol);
            case '}':
                return new DaxToken(DaxTokenType.CloseBrace, "}", startPos, startLine, startCol);
            case ',':
                return new DaxToken(DaxTokenType.Comma, ",", startPos, startLine, startCol);
            case '.':
                return new DaxToken(DaxTokenType.Dot, ".", startPos, startLine, startCol);
            case ';':
                return new DaxToken(DaxTokenType.Semicolon, ";", startPos, startLine, startCol);
            case '+':
                return new DaxToken(DaxTokenType.Plus, "+", startPos, startLine, startCol);
            case '-':
                return new DaxToken(DaxTokenType.Minus, "-", startPos, startLine, startCol);
            case '*':
                return new DaxToken(DaxTokenType.Star, "*", startPos, startLine, startCol);
            case '/':
                return new DaxToken(DaxTokenType.Slash, "/", startPos, startLine, startCol);
            case '^':
                return new DaxToken(DaxTokenType.Caret, "^", startPos, startLine, startCol);
            case '=':
                return new DaxToken(DaxTokenType.Equals, "=", startPos, startLine, startCol);
            case '<':
                if (_pos < _source.Length)
                {
                    if (_source[_pos] == '>')
                    {
                        _pos++; _col++;
                        return new DaxToken(DaxTokenType.NotEquals, "<>", startPos, startLine, startCol);
                    }
                    if (_source[_pos] == '=')
                    {
                        _pos++; _col++;
                        return new DaxToken(DaxTokenType.LessEquals, "<=", startPos, startLine, startCol);
                    }
                }
                return new DaxToken(DaxTokenType.LessThan, "<", startPos, startLine, startCol);
            case '>':
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    _pos++; _col++;
                    return new DaxToken(DaxTokenType.GreaterEquals, ">=", startPos, startLine, startCol);
                }
                return new DaxToken(DaxTokenType.GreaterThan, ">", startPos, startLine, startCol);
            case '&':
                if (_pos < _source.Length && _source[_pos] == '&')
                {
                    _pos++; _col++;
                    return new DaxToken(DaxTokenType.DoubleAmpersand, "&&", startPos, startLine, startCol);
                }
                return new DaxToken(DaxTokenType.Ampersand, "&", startPos, startLine, startCol);
            case '|':
                if (_pos < _source.Length && _source[_pos] == '|')
                {
                    _pos++; _col++;
                    return new DaxToken(DaxTokenType.DoublePipe, "||", startPos, startLine, startCol);
                }
                return new DaxToken(DaxTokenType.Error, "|", startPos, startLine, startCol);
            default:
                return new DaxToken(DaxTokenType.Error, c.ToString(), startPos, startLine, startCol);
        }
    }
}
