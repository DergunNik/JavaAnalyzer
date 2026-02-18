using System;
using System.Collections.Generic;

namespace JavaTranslator.Tokens;

public static class Keywords
{
    public static readonly HashSet<string> KeywordSet = new(StringComparer.Ordinal)
    {
        "abstract", "assert",
        "boolean", "break", "byte",
        "case", "catch", "char", "class", "continue",
        "default", "do", "double",
        "else", "enum", "extends",
        "final", "finally", "float", "for",
        "if", "implements", "import", "instanceof", "int", "interface",
        "long",
        "native", "new",
        "package", "private", "protected", "public",
        "return",
        "short", "static", "strictfp", "super", "switch", "synchronized",
        "this", "throw", "throws", "transient", "try",
        "void", "volatile",
        "while"
    };

    public static readonly HashSet<string> LiteralSet = new(StringComparer.Ordinal)
    {
        "true",
        "false",
        "null"   
    };

    public static readonly HashSet<string> OperatorSet = new(StringComparer.Ordinal)
    {
        "+", "-", "*", "/", "%",

        "++", "--",

        "==", "!=", "<", ">", "<=", ">=",

        "&&", "||", "!",

        "&", "|", "^", "~",
        "<<", ">>", ">>>",

        "=", "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^=",
        "<<=", ">>=", ">>>=",

        "?", ":", "->", "::", "."
    };

    public static readonly HashSet<string> SeparatorSet = new(StringComparer.Ordinal)
    {
        "(", ")",
        "{", "}",
        "[", "]",
        ";",     
        ",",     
        "..."    
    };

    
    public static bool IsKeyword(string value) =>
        KeywordSet.Contains(value);

    public static bool IsLiteral(string value) =>
        LiteralSet.Contains(value);

    public static bool IsOperator(string value) =>
        OperatorSet.Contains(value);

    public static bool IsSeparator(string value) =>
        SeparatorSet.Contains(value);
}
