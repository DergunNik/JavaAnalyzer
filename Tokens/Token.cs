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
        switch (Kind)
        {
            case TokenKind.EOF:
                return "EOF";
            case TokenKind.ERROR:
                return $"ERROR({Value})";
            case TokenKind.IDENTIFIER:
                return $"IDENTIFIER({Value})";
            case TokenKind.KEYWORD:
                return $"KEYWORD({Value})";
            case TokenKind.LITERAL:
                return $"LITERAL({Value})";
            case TokenKind.OPERATOR:
                return $"OPERATOR({Value})";
            case TokenKind.SEPARATOR:
                return $"SEPARATOR({Value})";
            default:
                return "ERROR(ToString method for this Kind is not implemented)";
        }

    }
}
