namespace TabularForge.DAXParser.Lexer;

public enum DaxTokenType
{
    // Literals
    Number,
    String,

    // Identifiers & References
    Identifier,
    TableReference,      // 'TableName'
    ColumnReference,     // [ColumnName]

    // Keywords
    Evaluate,
    Define,
    Measure,
    Column,
    Var,
    Return,
    Order,
    By,
    Asc,
    Desc,
    Start,
    At,
    In,
    Not,
    And,
    Or,
    True,
    False,
    Blank,
    Table,

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    Caret,
    Ampersand,
    Equals,
    NotEquals,           // <>
    LessThan,
    GreaterThan,
    LessEquals,
    GreaterEquals,
    DoubleAmpersand,     // &&
    DoublePipe,          // ||

    // Delimiters
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    OpenBrace,
    CloseBrace,
    Comma,
    Dot,
    Semicolon,

    // Trivia
    Whitespace,
    Newline,
    SingleLineComment,
    MultiLineComment,

    // Special
    EOF,
    Error
}
