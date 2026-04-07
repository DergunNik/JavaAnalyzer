using System;
using System.Collections.Generic;
using JavaTranslator.Tokens;
using JavaTranslator.Ast;

namespace JavaTranslator;

public class SyntaxException : Exception
{
    public SyntaxException(string message, Token token) 
        : base($"{message} at position {token.StartPos} (Token: {token.Value})") { }
}

public class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }
    
    private Token Current => Peek(0);
    private bool IsEOF => Current.Kind == TokenKind.EOF;

    private Token Peek(int offset)
    {
        var index = _pos + offset;
        if (index >= _tokens.Count) return _tokens[^1];
        return _tokens[index];
    }

    private Token Advance() => !IsEOF ? _tokens[_pos++] : _tokens[_pos];

    private Token Match(TokenKind expectedKind)
    {
        if (Current.Kind == expectedKind) return Advance();
        throw new SyntaxException($"Expected {expectedKind}, but found {Current.Kind}", Current);
    }

    private Token MatchValue(TokenKind kind, string expectedValue)
    {
        if (Current.Kind == kind && Current.Value == expectedValue) return Advance();
        throw new SyntaxException($"Expected '{expectedValue}', but found '{Current.Value}'", Current);
    }

    private bool Check(TokenKind kind, string value = null)
    {
        if (IsEOF) return false;
        if (Current.Kind != kind) return false;
        return value == null || Current.Value == value;
    }

    public CompilationUnitNode Parse()
    {
        var root = new CompilationUnitNode();
        while (Check(TokenKind.KEYWORD, "import")) root.Imports.Add(ParseImport());
        while (!IsEOF)
        {
            while (Check(TokenKind.KEYWORD, "public") || Check(TokenKind.KEYWORD, "private") || Check(TokenKind.KEYWORD, "protected") || Check(TokenKind.KEYWORD, "static"))
                Advance();

            if (Check(TokenKind.KEYWORD, "class")) root.Classes.Add(ParseClass());
            else Advance();
        }
        return root;
    }

    private ImportNode ParseImport()
    {
        MatchValue(TokenKind.KEYWORD, "import");
        var path = "";
        while (!Check(TokenKind.SEPARATOR, ";")) path += Advance().Value;
        MatchValue(TokenKind.SEPARATOR, ";");
        return new ImportNode { Path = path };
    }

    private ClassDeclNode ParseClass()
    {
        MatchValue(TokenKind.KEYWORD, "class");
        var name = Match(TokenKind.IDENTIFIER).Value;
        MatchValue(TokenKind.SEPARATOR, "{");
        var node = new ClassDeclNode { Name = name };
        while (!Check(TokenKind.SEPARATOR, "}")) node.Members.Add(ParseClassMember());
        MatchValue(TokenKind.SEPARATOR, "}");
        return node;
    }

    private AstNode ParseClassMember()
    {
        while (Check(TokenKind.KEYWORD, "public") || Check(TokenKind.KEYWORD, "private") || 
               Check(TokenKind.KEYWORD, "protected") || Check(TokenKind.KEYWORD, "static") || Check(TokenKind.KEYWORD, "final"))
            Advance();

        var type = Advance().Value;
        if (Check(TokenKind.SEPARATOR, "[")) { Advance(); MatchValue(TokenKind.SEPARATOR, "]"); type += "[]"; }
        var name = Match(TokenKind.IDENTIFIER).Value;

        if (Check(TokenKind.SEPARATOR, "("))
        {
            Advance();
            var method = new MethodDeclNode { ReturnType = type, Name = name };
            while (!Check(TokenKind.SEPARATOR, ")"))
            {
                var pType = Advance().Value;
                if (Check(TokenKind.SEPARATOR, "[")) { Advance(); MatchValue(TokenKind.SEPARATOR, "]"); pType += "[]"; }
                method.Parameters.Add(new ParameterNode { Type = pType, Name = Match(TokenKind.IDENTIFIER).Value });
                if (Check(TokenKind.SEPARATOR, ",")) Advance();
            }
            MatchValue(TokenKind.SEPARATOR, ")");
            method.Body = ParseBlock();
            return method;
        }
        ExpressionNode? init = null;
        if (Check(TokenKind.OPERATOR, "=")) { Advance(); init = ParseExpression(); }
        MatchValue(TokenKind.SEPARATOR, ";");
        return new VariableDeclNode { Type = type, Name = name, Initializer = init };
    }

    private BlockStatementNode ParseBlock()
    {
        MatchValue(TokenKind.SEPARATOR, "{");
        var block = new BlockStatementNode();
        while (!Check(TokenKind.SEPARATOR, "}")) block.Statements.Add(ParseStatement());
        MatchValue(TokenKind.SEPARATOR, "}");
        return block;
    }

    private StatementNode ParseStatement()
    {
        if (Check(TokenKind.SEPARATOR, "{")) return ParseBlock();
        if (Check(TokenKind.KEYWORD, "if")) return ParseIf();
        if (Check(TokenKind.KEYWORD, "while")) return ParseWhile();
        if (Check(TokenKind.KEYWORD, "do")) return ParseDoWhile();
        if (Check(TokenKind.KEYWORD, "for")) return ParseFor();
        if (Check(TokenKind.KEYWORD, "return")) return ParseReturn();
        if (Check(TokenKind.KEYWORD, "break")) { Advance(); MatchValue(TokenKind.SEPARATOR, ";"); return new BreakStatementNode(); }
        if (Check(TokenKind.KEYWORD, "continue")) { Advance(); MatchValue(TokenKind.SEPARATOR, ";"); return new ContinueStatementNode(); }
        if (IsVarDecl()) return ParseVarDeclStmt();

        var expr = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ";");
        return new ExpressionStatementNode { Expression = expr };
    }

    private IfStatementNode ParseIf()
    {
        Advance(); MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        var then = ParseStatement();
        StatementNode? els = null;
        if (Check(TokenKind.KEYWORD, "else")) { Advance(); els = ParseStatement(); }
        return new IfStatementNode { Condition = cond, ThenBranch = then, ElseBranch = els };
    }

    private WhileStatementNode ParseWhile()
    {
        Advance(); MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        return new WhileStatementNode { Condition = cond, Body = ParseStatement() };
    }

    private DoWhileStatementNode ParseDoWhile()
    {
        Advance(); var body = ParseStatement();
        MatchValue(TokenKind.KEYWORD, "while");
        MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        MatchValue(TokenKind.SEPARATOR, ";");
        return new DoWhileStatementNode { Body = body, Condition = cond };
    }

    private StatementNode ParseFor()
    {
        Advance(); MatchValue(TokenKind.SEPARATOR, "(");
        AstNode? init = null;
        if (!Check(TokenKind.SEPARATOR, ";")) init = IsVarDecl() ? ParseVarDeclStmt(false) : ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ";");
        ExpressionNode? cond = !Check(TokenKind.SEPARATOR, ";") ? ParseExpression() : null;
        MatchValue(TokenKind.SEPARATOR, ";");
        ExpressionNode? inc = !Check(TokenKind.SEPARATOR, ")") ? ParseExpression() : null;
        MatchValue(TokenKind.SEPARATOR, ")");
        return new ForStatementNode { Initialization = init, Condition = cond, Increment = inc, Body = ParseStatement() };
    }

    private ReturnStatementNode ParseReturn()
    {
        Advance();
        ExpressionNode? val = !Check(TokenKind.SEPARATOR, ";") ? ParseExpression() : null;
        MatchValue(TokenKind.SEPARATOR, ";");
        return new ReturnStatementNode { Value = val };
    }

    private bool IsVarDecl() => IsType(Current.Value) || (Current.Kind == TokenKind.IDENTIFIER && Peek(1).Kind == TokenKind.IDENTIFIER);

    private VariableDeclNode ParseVarDeclStmt(bool semi = true)
    {
        var type = Advance().Value;
        if (Check(TokenKind.SEPARATOR, "[")) { Advance(); MatchValue(TokenKind.SEPARATOR, "]"); type += "[]"; }
        var name = Match(TokenKind.IDENTIFIER).Value;
        ExpressionNode? init = null;
        if (Check(TokenKind.OPERATOR, "=")) { Advance(); init = ParseExpression(); }
        if (semi) MatchValue(TokenKind.SEPARATOR, ";");
        return new VariableDeclNode { Type = type, Name = name, Initializer = init };
    }

    private ExpressionNode ParseExpression() => ParseAssignment();

    private ExpressionNode ParseAssignment()
    {
        var expr = ParseBinary(0);
        if (Check(TokenKind.OPERATOR) && Current.Value.Contains("="))
        {
            var op = Advance().Value;
            return new AssignmentExpressionNode { Target = expr, Operator = op, Value = ParseAssignment() };
        }
        return expr;
    }

    private ExpressionNode ParseBinary(int prec)
    {
        var expr = ParseUnary();
        while (true)
        {
            var op = Current.Value;
            var nextPrec = GetPrec(op);
            if (nextPrec <= prec) break;
            Advance();
            expr = new BinaryExpressionNode { Left = expr, Operator = op, Right = ParseBinary(nextPrec) };
        }
        return expr;
    }

    private int GetPrec(string op) => op switch { "||" => 1, "&&" => 2, "==" or "!=" => 3, "<" or ">" or "<=" or ">=" => 4, "+" or "-" => 5, "*" or "/" or "%" => 6, _ => 0 };

    private ExpressionNode ParseUnary()
    {
        if (Check(TokenKind.OPERATOR) && (Current.Value == "!" || Current.Value == "-" || Current.Value == "++" || Current.Value == "--"))
            return new UnaryExpressionNode { Operator = Advance().Value, Operand = ParseUnary(), IsPostfix = false };
        return ParsePostfix();
    }

    private ExpressionNode ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Check(TokenKind.OPERATOR, ".")) { Advance(); expr = new MemberAccessExpressionNode { Target = expr, MemberName = Match(TokenKind.IDENTIFIER).Value }; }
            else if (Check(TokenKind.SEPARATOR, "("))
            {
                Advance(); var call = new MethodCallExpressionNode { Target = expr };
                if (!Check(TokenKind.SEPARATOR, ")"))
                {
                    do { call.Arguments.Add(ParseExpression()); } while (Check(TokenKind.SEPARATOR, ",") && Advance() != null);
                }
                MatchValue(TokenKind.SEPARATOR, ")"); expr = call;
            }
            else if (Check(TokenKind.OPERATOR, "++") || Check(TokenKind.OPERATOR, "--"))
                expr = new UnaryExpressionNode { Operator = Advance().Value, Operand = expr, IsPostfix = true };
            else break;
        }
        return expr;
    }

    private ExpressionNode ParsePrimary()
    {
        if (Check(TokenKind.LITERAL)) return new LiteralExpressionNode { Value = Advance().Value };
        if (Check(TokenKind.IDENTIFIER)) return new IdentifierExpressionNode { Name = Advance().Value };
        if (Check(TokenKind.SEPARATOR, "(")) { Advance(); var e = ParseExpression(); MatchValue(TokenKind.SEPARATOR, ")"); return e; }
        if (Check(TokenKind.KEYWORD, "new"))
        {
            Advance(); var type = Advance().Value; MatchValue(TokenKind.SEPARATOR, "(");
            var obj = new ObjectCreationExpressionNode { Type = type };
            if (!Check(TokenKind.SEPARATOR, ")"))
            {
                do { obj.Arguments.Add(ParseExpression()); } while (Check(TokenKind.SEPARATOR, ",") && Advance() != null);
            }
            MatchValue(TokenKind.SEPARATOR, ")"); return obj;
        }
        throw new SyntaxException($"Unexpected token {Current.Value}", Current);
    }

    private bool IsType(string v) => v is "int" or "long" or "double" or "float" or "char" or "boolean" or "byte" or "short" or "void" or "var" or "String" or "Scanner";
}