using TabularForge.DAXParser.Lexer;

namespace TabularForge.DAXParser.Parser;

public sealed class DaxParser
{
    private readonly List<DaxToken> _tokens;
    private int _pos;
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Errors => _errors;

    public DaxParser(List<DaxToken> tokens)
    {
        // Filter out trivia for parsing
        _tokens = tokens.Where(t => !t.IsTrivia).ToList();
    }

    public DaxScriptNode Parse()
    {
        var script = new DaxScriptNode();
        if (_tokens.Count > 0)
        {
            script.StartOffset = _tokens[0].StartOffset;
            script.Line = _tokens[0].Line;
            script.Column = _tokens[0].Column;
        }

        while (!IsAtEnd())
        {
            var stmt = ParseStatement();
            if (stmt != null)
            {
                script.Statements.Add(stmt);
            }
            else
            {
                // Skip unrecognized token
                Advance();
            }
        }

        script.EndOffset = _pos < _tokens.Count ? _tokens[_pos].EndOffset :
            (_tokens.Count > 0 ? _tokens[^1].EndOffset : 0);
        return script;
    }

    private DaxAstNode? ParseStatement()
    {
        if (IsAtEnd()) return null;

        var token = Current();
        return token.Type switch
        {
            DaxTokenType.Define => ParseDefine(),
            DaxTokenType.Evaluate => ParseEvaluate(),
            DaxTokenType.Measure => ParseMeasureDef(),
            DaxTokenType.Column => ParseColumnDef(),
            DaxTokenType.Var => ParseVar(),
            _ => ParseExpression()
        };
    }

    private DaxDefineNode ParseDefine()
    {
        var node = new DaxDefineNode();
        SetPosition(node, Current());
        Advance(); // skip DEFINE

        while (!IsAtEnd() && !Check(DaxTokenType.Evaluate))
        {
            if (Check(DaxTokenType.Measure))
            {
                var meas = ParseMeasureDef();
                if (meas != null) node.Definitions.Add(meas);
            }
            else if (Check(DaxTokenType.Column))
            {
                var col = ParseColumnDef();
                if (col != null) node.Definitions.Add(col);
            }
            else if (Check(DaxTokenType.Var))
            {
                var v = ParseVar();
                if (v != null) node.Definitions.Add(v);
            }
            else if (Check(DaxTokenType.Table))
            {
                // TABLE definition in DEFINE block
                Advance();
                var expr = ParseExpression();
                if (expr != null) node.Definitions.Add(expr);
            }
            else
            {
                break;
            }
        }

        if (Check(DaxTokenType.Evaluate))
        {
            node.Evaluate = ParseEvaluate();
        }

        node.EndOffset = PreviousEndOffset();
        return node;
    }

    private DaxEvaluateNode ParseEvaluate()
    {
        var node = new DaxEvaluateNode();
        SetPosition(node, Current());
        Advance(); // skip EVALUATE

        node.Expression = ParseExpression();
        node.EndOffset = PreviousEndOffset();
        return node;
    }

    private DaxMeasureDefNode? ParseMeasureDef()
    {
        var node = new DaxMeasureDefNode();
        SetPosition(node, Current());
        Advance(); // skip MEASURE

        // Expect 'Table'[Measure] = expression
        if (Check(DaxTokenType.TableReference))
        {
            node.TableName = ExtractName(Current().Text, '\'');
            Advance();
        }
        if (Check(DaxTokenType.ColumnReference))
        {
            node.MeasureName = ExtractName(Current().Text, '[', ']');
            Advance();
        }
        if (Check(DaxTokenType.Equals))
        {
            Advance();
        }

        node.Expression = ParseExpression();
        node.EndOffset = PreviousEndOffset();
        return node;
    }

    private DaxColumnDefNode? ParseColumnDef()
    {
        var node = new DaxColumnDefNode();
        SetPosition(node, Current());
        Advance(); // skip COLUMN

        if (Check(DaxTokenType.TableReference))
        {
            node.TableName = ExtractName(Current().Text, '\'');
            Advance();
        }
        if (Check(DaxTokenType.ColumnReference))
        {
            node.ColumnName = ExtractName(Current().Text, '[', ']');
            Advance();
        }
        if (Check(DaxTokenType.Equals))
        {
            Advance();
        }

        node.Expression = ParseExpression();
        node.EndOffset = PreviousEndOffset();
        return node;
    }

    private DaxVarNode? ParseVar()
    {
        var node = new DaxVarNode();
        SetPosition(node, Current());
        Advance(); // skip VAR

        if (Check(DaxTokenType.Identifier))
        {
            node.VariableName = Current().Text;
            Advance();
        }
        if (Check(DaxTokenType.Equals))
        {
            Advance();
        }

        node.Value = ParseExpression();
        node.EndOffset = PreviousEndOffset();
        return node;
    }

    private DaxAstNode? ParseExpression()
    {
        return ParseOr();
    }

    private DaxAstNode? ParseOr()
    {
        var left = ParseAnd();
        while (Check(DaxTokenType.DoublePipe) || Check(DaxTokenType.Or))
        {
            var op = Current().Text;
            Advance();
            var right = ParseAnd();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseAnd()
    {
        var left = ParseComparison();
        while (Check(DaxTokenType.DoubleAmpersand) || Check(DaxTokenType.And))
        {
            var op = Current().Text;
            Advance();
            var right = ParseComparison();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseComparison()
    {
        var left = ParseConcatenation();
        if (Check(DaxTokenType.Equals) || Check(DaxTokenType.NotEquals) ||
            Check(DaxTokenType.LessThan) || Check(DaxTokenType.GreaterThan) ||
            Check(DaxTokenType.LessEquals) || Check(DaxTokenType.GreaterEquals))
        {
            var op = Current().Text;
            Advance();
            var right = ParseConcatenation();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseConcatenation()
    {
        var left = ParseAddition();
        while (Check(DaxTokenType.Ampersand))
        {
            var op = Current().Text;
            Advance();
            var right = ParseAddition();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseAddition()
    {
        var left = ParseMultiplication();
        while (Check(DaxTokenType.Plus) || Check(DaxTokenType.Minus))
        {
            var op = Current().Text;
            Advance();
            var right = ParseMultiplication();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseMultiplication()
    {
        var left = ParseExponent();
        while (Check(DaxTokenType.Star) || Check(DaxTokenType.Slash))
        {
            var op = Current().Text;
            Advance();
            var right = ParseExponent();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseExponent()
    {
        var left = ParseUnary();
        if (Check(DaxTokenType.Caret))
        {
            var op = Current().Text;
            Advance();
            var right = ParseUnary();
            left = new DaxBinaryOpNode
            {
                Left = left,
                Operator = op,
                Right = right,
                StartOffset = left?.StartOffset ?? 0,
                EndOffset = PreviousEndOffset()
            };
        }
        return left;
    }

    private DaxAstNode? ParseUnary()
    {
        if (Check(DaxTokenType.Minus) || Check(DaxTokenType.Not))
        {
            var op = Current().Text;
            var start = Current().StartOffset;
            Advance();
            var operand = ParseUnary();
            return new DaxUnaryOpNode
            {
                Operator = op,
                Operand = operand,
                StartOffset = start,
                EndOffset = PreviousEndOffset()
            };
        }
        return ParsePrimary();
    }

    private DaxAstNode? ParsePrimary()
    {
        if (IsAtEnd()) return null;

        var token = Current();

        switch (token.Type)
        {
            case DaxTokenType.Number:
            case DaxTokenType.String:
            case DaxTokenType.True:
            case DaxTokenType.False:
            case DaxTokenType.Blank:
                Advance();
                return new DaxLiteralNode
                {
                    LiteralType = token.Type,
                    Value = token.Text,
                    StartOffset = token.StartOffset,
                    EndOffset = token.EndOffset,
                    Line = token.Line,
                    Column = token.Column
                };

            case DaxTokenType.TableReference:
                Advance();
                var tableRef = new DaxTableRefNode
                {
                    TableName = ExtractName(token.Text, '\''),
                    StartOffset = token.StartOffset,
                    EndOffset = token.EndOffset,
                    Line = token.Line,
                    Column = token.Column
                };
                // Check for column reference following table ref
                if (Check(DaxTokenType.ColumnReference))
                {
                    var colToken = Current();
                    Advance();
                    return new DaxColumnRefNode
                    {
                        TableName = tableRef.TableName,
                        ColumnName = ExtractName(colToken.Text, '[', ']'),
                        StartOffset = token.StartOffset,
                        EndOffset = colToken.EndOffset,
                        Line = token.Line,
                        Column = token.Column
                    };
                }
                return tableRef;

            case DaxTokenType.ColumnReference:
                Advance();
                return new DaxColumnRefNode
                {
                    ColumnName = ExtractName(token.Text, '[', ']'),
                    StartOffset = token.StartOffset,
                    EndOffset = token.EndOffset,
                    Line = token.Line,
                    Column = token.Column
                };

            case DaxTokenType.Identifier:
                return ParseIdentifierOrFunctionCall();

            case DaxTokenType.Var:
                return ParseVar();

            case DaxTokenType.Return:
                Advance();
                var retExpr = ParseExpression();
                return new DaxReturnNode
                {
                    Expression = retExpr,
                    StartOffset = token.StartOffset,
                    EndOffset = PreviousEndOffset(),
                    Line = token.Line,
                    Column = token.Column
                };

            case DaxTokenType.OpenParen:
                Advance();
                var inner = ParseExpression();
                if (Check(DaxTokenType.CloseParen))
                    Advance();
                return inner;

            case DaxTokenType.OpenBrace:
                return ParseTableConstructor();

            default:
                // Check if this is a keyword used as identifier (e.g., function name)
                if (token.IsKeyword)
                {
                    return ParseIdentifierOrFunctionCall();
                }
                return null;
        }
    }

    private DaxAstNode? ParseIdentifierOrFunctionCall()
    {
        var token = Current();
        Advance();

        // Check if this is a function call
        if (Check(DaxTokenType.OpenParen))
        {
            Advance(); // skip (
            var funcNode = new DaxFunctionCallNode
            {
                FunctionName = token.Text,
                StartOffset = token.StartOffset,
                Line = token.Line,
                Column = token.Column
            };

            if (!Check(DaxTokenType.CloseParen))
            {
                var arg = ParseExpression();
                if (arg != null) funcNode.Arguments.Add(arg);

                while (Check(DaxTokenType.Comma))
                {
                    Advance();
                    arg = ParseExpression();
                    if (arg != null) funcNode.Arguments.Add(arg);
                }
            }

            if (Check(DaxTokenType.CloseParen))
                Advance();

            funcNode.EndOffset = PreviousEndOffset();
            return funcNode;
        }

        // Check for column reference after identifier (table[column])
        if (Check(DaxTokenType.ColumnReference))
        {
            var colToken = Current();
            Advance();
            return new DaxColumnRefNode
            {
                TableName = token.Text,
                ColumnName = ExtractName(colToken.Text, '[', ']'),
                StartOffset = token.StartOffset,
                EndOffset = colToken.EndOffset,
                Line = token.Line,
                Column = token.Column
            };
        }

        return new DaxIdentifierNode
        {
            Name = token.Text,
            StartOffset = token.StartOffset,
            EndOffset = token.EndOffset,
            Line = token.Line,
            Column = token.Column
        };
    }

    private DaxAstNode ParseTableConstructor()
    {
        var node = new DaxExpressionNode();
        SetPosition(node, Current());
        Advance(); // skip {

        while (!IsAtEnd() && !Check(DaxTokenType.CloseBrace))
        {
            var expr = ParseExpression();
            if (expr != null) node.Children.Add(expr);
            if (Check(DaxTokenType.Comma))
                Advance();
            else
                break;
        }

        if (Check(DaxTokenType.CloseBrace))
            Advance();

        node.EndOffset = PreviousEndOffset();
        return node;
    }

    // --- Helpers ---

    private DaxToken Current() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];
    private bool IsAtEnd() => _pos >= _tokens.Count || Current().Type == DaxTokenType.EOF;
    private bool Check(DaxTokenType type) => !IsAtEnd() && Current().Type == type;

    private void Advance()
    {
        if (!IsAtEnd()) _pos++;
    }

    private int PreviousEndOffset()
    {
        if (_pos > 0 && _pos <= _tokens.Count)
            return _tokens[_pos - 1].EndOffset;
        return _tokens.Count > 0 ? _tokens[^1].EndOffset : 0;
    }

    private static void SetPosition(DaxAstNode node, DaxToken token)
    {
        node.StartOffset = token.StartOffset;
        node.Line = token.Line;
        node.Column = token.Column;
    }

    private static string ExtractName(string text, char delimiter)
    {
        if (text.Length >= 2 && text[0] == delimiter && text[^1] == delimiter)
            return text[1..^1];
        return text;
    }

    private static string ExtractName(string text, char open, char close)
    {
        if (text.Length >= 2 && text[0] == open && text[^1] == close)
            return text[1..^1];
        return text;
    }
}
