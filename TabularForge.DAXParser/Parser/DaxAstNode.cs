using TabularForge.DAXParser.Lexer;

namespace TabularForge.DAXParser.Parser;

public abstract class DaxAstNode
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

public sealed class DaxExpressionNode : DaxAstNode
{
    public List<DaxAstNode> Children { get; } = new();
}

public sealed class DaxFunctionCallNode : DaxAstNode
{
    public string FunctionName { get; set; } = string.Empty;
    public List<DaxAstNode> Arguments { get; } = new();
}

public sealed class DaxTableRefNode : DaxAstNode
{
    public string TableName { get; set; } = string.Empty;
}

public sealed class DaxColumnRefNode : DaxAstNode
{
    public string? TableName { get; set; }
    public string ColumnName { get; set; } = string.Empty;
}

public sealed class DaxLiteralNode : DaxAstNode
{
    public DaxTokenType LiteralType { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class DaxBinaryOpNode : DaxAstNode
{
    public DaxAstNode? Left { get; set; }
    public string Operator { get; set; } = string.Empty;
    public DaxAstNode? Right { get; set; }
}

public sealed class DaxUnaryOpNode : DaxAstNode
{
    public string Operator { get; set; } = string.Empty;
    public DaxAstNode? Operand { get; set; }
}

public sealed class DaxVarNode : DaxAstNode
{
    public string VariableName { get; set; } = string.Empty;
    public DaxAstNode? Value { get; set; }
}

public sealed class DaxReturnNode : DaxAstNode
{
    public DaxAstNode? Expression { get; set; }
}

public sealed class DaxMeasureDefNode : DaxAstNode
{
    public string TableName { get; set; } = string.Empty;
    public string MeasureName { get; set; } = string.Empty;
    public DaxAstNode? Expression { get; set; }
}

public sealed class DaxColumnDefNode : DaxAstNode
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public DaxAstNode? Expression { get; set; }
}

public sealed class DaxEvaluateNode : DaxAstNode
{
    public DaxAstNode? Expression { get; set; }
}

public sealed class DaxDefineNode : DaxAstNode
{
    public List<DaxAstNode> Definitions { get; } = new();
    public DaxEvaluateNode? Evaluate { get; set; }
}

public sealed class DaxScriptNode : DaxAstNode
{
    public List<DaxAstNode> Statements { get; } = new();
}

public sealed class DaxIdentifierNode : DaxAstNode
{
    public string Name { get; set; } = string.Empty;
}

public sealed class DaxErrorNode : DaxAstNode
{
    public string Message { get; set; } = string.Empty;
    public DaxToken? Token { get; set; }
}
