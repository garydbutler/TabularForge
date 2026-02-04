using TabularForge.DAXParser.Lexer;
using TabularForge.DAXParser.Parser;

namespace TabularForge.DAXParser.Semantics;

public sealed class ModelInfo
{
    public List<TableInfo> Tables { get; set; } = new();

    public TableInfo? FindTable(string name)
    {
        return Tables.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public ColumnInfo? FindColumn(string tableName, string columnName)
    {
        var table = FindTable(tableName);
        return table?.FindColumn(columnName);
    }

    public MeasureInfo? FindMeasure(string name)
    {
        return Tables.SelectMany(t => t.Measures)
            .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public MeasureInfo? FindMeasure(string tableName, string measureName)
    {
        var table = FindTable(tableName);
        return table?.Measures.FirstOrDefault(m =>
            m.Name.Equals(measureName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class TableInfo
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<MeasureInfo> Measures { get; set; } = new();

    public ColumnInfo? FindColumn(string name)
    {
        return Columns.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

public sealed class MeasureInfo
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

public sealed class DaxSemanticAnalyzer
{
    private readonly ModelInfo _modelInfo;
    private readonly List<DaxDiagnostic> _diagnostics = new();
    private readonly Dictionary<string, string> _variables = new();

    public DaxSemanticAnalyzer(ModelInfo modelInfo)
    {
        _modelInfo = modelInfo;
    }

    public List<DaxDiagnostic> Analyze(string daxExpression, string? sourceObjectName = null)
    {
        _diagnostics.Clear();
        _variables.Clear();

        var lexer = new DaxLexer(daxExpression);
        List<DaxToken> tokens;
        try
        {
            tokens = lexer.Tokenize();
        }
        catch (Exception ex)
        {
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                $"Lexer error: {ex.Message}", 1, 1));
            return _diagnostics;
        }

        // Check for error tokens
        foreach (var token in tokens.Where(t => t.Type == DaxTokenType.Error))
        {
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                $"Unexpected character: '{token.Text}'",
                token.Line, token.Column, token.StartOffset, token.Text.Length));
        }

        // Parse
        var parser = new DaxParser(tokens);
        var ast = parser.Parse();

        foreach (var err in parser.Errors)
        {
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error, err, 1, 1));
        }

        // Semantic analysis via AST walk
        foreach (var stmt in ast.Statements)
        {
            AnalyzeNode(stmt);
        }

        // Check for unmatched brackets via token scan
        CheckBracketBalance(tokens);

        // Set source on all diagnostics
        if (sourceObjectName != null)
        {
            foreach (var d in _diagnostics)
                d.Source = sourceObjectName;
        }

        return _diagnostics;
    }

    private void AnalyzeNode(DaxAstNode? node)
    {
        if (node == null) return;

        switch (node)
        {
            case DaxTableRefNode tableRef:
                ValidateTableReference(tableRef);
                break;

            case DaxColumnRefNode colRef:
                ValidateColumnReference(colRef);
                break;

            case DaxFunctionCallNode funcCall:
                ValidateFunctionCall(funcCall);
                foreach (var arg in funcCall.Arguments)
                    AnalyzeNode(arg);
                break;

            case DaxVarNode varNode:
                _variables[varNode.VariableName] = "Variant";
                AnalyzeNode(varNode.Value);
                break;

            case DaxReturnNode returnNode:
                AnalyzeNode(returnNode.Expression);
                break;

            case DaxBinaryOpNode binOp:
                AnalyzeNode(binOp.Left);
                AnalyzeNode(binOp.Right);
                break;

            case DaxUnaryOpNode unaryOp:
                AnalyzeNode(unaryOp.Operand);
                break;

            case DaxMeasureDefNode measureDef:
                AnalyzeNode(measureDef.Expression);
                break;

            case DaxColumnDefNode columnDef:
                AnalyzeNode(columnDef.Expression);
                break;

            case DaxEvaluateNode evalNode:
                AnalyzeNode(evalNode.Expression);
                break;

            case DaxDefineNode defineNode:
                foreach (var def in defineNode.Definitions)
                    AnalyzeNode(def);
                if (defineNode.Evaluate != null)
                    AnalyzeNode(defineNode.Evaluate);
                break;

            case DaxScriptNode scriptNode:
                foreach (var stmt in scriptNode.Statements)
                    AnalyzeNode(stmt);
                break;

            case DaxExpressionNode exprNode:
                foreach (var child in exprNode.Children)
                    AnalyzeNode(child);
                break;

            case DaxIdentifierNode identNode:
                ValidateIdentifier(identNode);
                break;
        }
    }

    private void ValidateTableReference(DaxTableRefNode tableRef)
    {
        if (_modelInfo.FindTable(tableRef.TableName) == null)
        {
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                $"Table '{tableRef.TableName}' not found in model",
                tableRef.Line, tableRef.Column, tableRef.StartOffset,
                tableRef.EndOffset - tableRef.StartOffset));
        }
    }

    private void ValidateColumnReference(DaxColumnRefNode colRef)
    {
        if (colRef.TableName != null)
        {
            var table = _modelInfo.FindTable(colRef.TableName);
            if (table == null)
            {
                _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                    $"Table '{colRef.TableName}' not found in model",
                    colRef.Line, colRef.Column, colRef.StartOffset,
                    colRef.EndOffset - colRef.StartOffset));
                return;
            }

            // Check if it's a column or measure
            var column = table.FindColumn(colRef.ColumnName);
            var measure = table.Measures.FirstOrDefault(m =>
                m.Name.Equals(colRef.ColumnName, StringComparison.OrdinalIgnoreCase));

            if (column == null && measure == null)
            {
                _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                    $"Column or measure '{colRef.ColumnName}' not found in table '{colRef.TableName}'",
                    colRef.Line, colRef.Column, colRef.StartOffset,
                    colRef.EndOffset - colRef.StartOffset));
            }
        }
        else
        {
            // Unqualified column reference - check all tables
            var found = _modelInfo.Tables.Any(t =>
                t.FindColumn(colRef.ColumnName) != null ||
                t.Measures.Any(m => m.Name.Equals(colRef.ColumnName, StringComparison.OrdinalIgnoreCase)));

            if (!found)
            {
                _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Warning,
                    $"Column or measure '[{colRef.ColumnName}]' not found in any table. Is the reference correct?",
                    colRef.Line, colRef.Column, colRef.StartOffset,
                    colRef.EndOffset - colRef.StartOffset));
            }
        }
    }

    private void ValidateFunctionCall(DaxFunctionCallNode funcCall)
    {
        if (!DaxFunctionCatalog.TryGetFunction(funcCall.FunctionName, out var signature))
        {
            // Check if it's a variable reference that looks like a function call
            if (!_variables.ContainsKey(funcCall.FunctionName))
            {
                _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Warning,
                    $"Unknown function '{funcCall.FunctionName}'. Is this a custom function or variable?",
                    funcCall.Line, funcCall.Column, funcCall.StartOffset,
                    funcCall.FunctionName.Length));
            }
            return;
        }

        // Check required parameter count
        var requiredParams = signature!.Parameters.Count(p => !p.IsOptional);
        if (funcCall.Arguments.Count < requiredParams)
        {
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                $"Function '{funcCall.FunctionName}' requires at least {requiredParams} argument(s), but {funcCall.Arguments.Count} provided",
                funcCall.Line, funcCall.Column, funcCall.StartOffset,
                funcCall.FunctionName.Length));
        }
    }

    private void ValidateIdentifier(DaxIdentifierNode identNode)
    {
        // Check if it's a known variable, table, or function
        if (_variables.ContainsKey(identNode.Name)) return;
        if (_modelInfo.FindTable(identNode.Name) != null) return;
        if (DaxFunctionCatalog.TryGetFunction(identNode.Name, out _)) return;
        if (_modelInfo.FindMeasure(identNode.Name) != null) return;

        // Could be a valid identifier we don't know about - just info level
        // Don't flag keywords or common values
        var name = identNode.Name.ToUpperInvariant();
        if (name is "TRUE" or "FALSE" or "BLANK" or "NULL") return;
    }

    private void CheckBracketBalance(List<DaxToken> tokens)
    {
        var stack = new Stack<DaxToken>();
        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case DaxTokenType.OpenParen:
                case DaxTokenType.OpenBrace:
                    stack.Push(token);
                    break;
                case DaxTokenType.CloseParen:
                    if (stack.Count == 0 || stack.Peek().Type != DaxTokenType.OpenParen)
                    {
                        _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                            "Unmatched closing parenthesis ')'",
                            token.Line, token.Column, token.StartOffset, 1));
                    }
                    else
                    {
                        stack.Pop();
                    }
                    break;
                case DaxTokenType.CloseBrace:
                    if (stack.Count == 0 || stack.Peek().Type != DaxTokenType.OpenBrace)
                    {
                        _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                            "Unmatched closing brace '}'",
                            token.Line, token.Column, token.StartOffset, 1));
                    }
                    else
                    {
                        stack.Pop();
                    }
                    break;
            }
        }

        while (stack.Count > 0)
        {
            var unmatched = stack.Pop();
            var bracket = unmatched.Type == DaxTokenType.OpenParen ? "(" : "{";
            _diagnostics.Add(new DaxDiagnostic(DaxDiagnosticSeverity.Error,
                $"Unmatched opening bracket '{bracket}'",
                unmatched.Line, unmatched.Column, unmatched.StartOffset, 1));
        }
    }
}
