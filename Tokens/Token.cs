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
            TokenKind.ERROR => $"ERR({Value})",
            TokenKind.IDENTIFIER => $"ID({Value})",
            TokenKind.KEYWORD => $"KEY({Value})",
            TokenKind.LITERAL => $"LIT({Value})",
            TokenKind.OPERATOR => $"OP({Value})",
            TokenKind.SEPARATOR => $"SEP({Value})",
            _ => "ERR(ToString method for this Kind is not implemented)"
        };
    }
}

class JavaNullType { }

public record LiteralToken(TokenKind Kind, string Value, int StartPos, bool IsFromNewLine, Type Type) : Token(Kind, Value, StartPos, IsFromNewLine)
{
    public override string ToString()
    {
        var javaType = Type switch
        {
            null => "unknown",

            _ when Type == typeof(JavaNullType) => "null",

            _ when Type == typeof(int)    || Type.FullName == "System.Int32"   => "int",
            _ when Type == typeof(long)   || Type.FullName == "System.Int64"   => "long",
            _ when Type == typeof(float)  || Type.FullName == "System.Single"  => "float",
            _ when Type == typeof(double) || Type.FullName == "System.Double"  => "double",
            _ when Type == typeof(char)   || Type.FullName == "System.Char"    => "char",
            _ when Type == typeof(bool)   || Type.FullName == "System.Boolean" => "boolean",

            _ when Type == typeof(string) || Type.FullName == "System.String"  => "String",
            _ when Type == typeof(object) || Type.FullName == "System.Object"  => "Object",

            _ => Type.Name
        };

        return $"LIT({Value}, {javaType})";
    }
}