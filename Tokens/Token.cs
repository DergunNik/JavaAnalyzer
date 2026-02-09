using System;
using System.Collections.Generic;
using System.Text;

namespace JavaTranslator.Tokens;

public enum TokenKind
{
    EOF,
    ERROR,
    IDENTIFIER,
    KEYWORD,
    LITERAL,
    OPERATOR,
    SEPARATOR
}

public record Token(TokenKind Kind, string Value, int StartPos, bool IsFromNewLine)
{
    public override string ToString()
    {
        return Kind switch
        {
            TokenKind.EOF => "EOF",
            TokenKind.ERROR => $"ERROR({Value})",
            TokenKind.IDENTIFIER => $"IDENTIFIER({Value})",
            TokenKind.KEYWORD => $"KEYWORD({Value})",
            TokenKind.LITERAL => $"LITERAL({Value})",
            TokenKind.OPERATOR => $"OPERATOR({Value})",
            TokenKind.SEPARATOR => $"SEPARATOR({Value})",
            _ => "ERROR(ToString method for this Kind is not implemented)"
        };
    }
}

public record LiteralToken(TokenKind Kind, string Value, int StartPos, bool IsFromNewLine, Type Type) : Token(Kind, Value, StartPos, IsFromNewLine)
{
    public override string ToString()
    {
        return $"LITERAL({Value}, {Type})";
    }
}