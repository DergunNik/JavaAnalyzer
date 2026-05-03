using System;
using System.Collections.Generic;
using System.Text;
using JavaTranslator.Ast;
using JavaTranslator.Tokens;

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
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Errors => _errors;

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

    private Token PeekAt(int index)
    {
        if (index >= _tokens.Count) return _tokens[^1];
        return _tokens[index];
    }

    private Token Advance() => !IsEOF ? _tokens[_pos++] : _tokens[_pos];

    private Token AdvanceAt(ref int index)
    {
        var token = PeekAt(index);
        if (token.Kind != TokenKind.EOF) index++;
        return token;
    }

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

    private bool Check(TokenKind kind, string? value = null)
    {
        if (IsEOF) return false;
        if (Current.Kind != kind) return false;
        return value == null || Current.Value == value;
    }

    private bool CheckAt(int index, TokenKind kind, string? value = null)
    {
        var token = PeekAt(index);
        if (token.Kind == TokenKind.EOF) return false;
        if (token.Kind != kind) return false;
        return value == null || token.Value == value;
    }

    private void Report(SyntaxException ex)
    {
        _errors.Add(ex.Message);
    }

    public CompilationUnitNode Parse()
    {
        var root = new CompilationUnitNode();

        while (Check(TokenKind.KEYWORD, "import"))
        {
            try
            {
                root.Imports.Add(ParseImport());
            }
            catch (SyntaxException ex)
            {
                Report(ex);
                SynchronizeToTopLevelBoundary();
            }
        }

        while (!IsEOF)
        {
            try
            {
                while (Check(TokenKind.KEYWORD, "public") || Check(TokenKind.KEYWORD, "static"))
                    Advance();

                if (Check(TokenKind.KEYWORD, "class"))
                    root.Classes.Add(ParseClass());
                else if (!IsEOF)
                    Advance();
            }
            catch (SyntaxException ex)
            {
                Report(ex);
                SynchronizeToTopLevelBoundary();
            }
        }

        return root;
    }

    private ImportNode ParseImport()
    {
        MatchValue(TokenKind.KEYWORD, "import");
        var path = new StringBuilder();

        while (!Check(TokenKind.SEPARATOR, ";"))
        {
            if (IsEOF)
                throw new SyntaxException("Unterminated import statement", Current);

            path.Append(Advance().Value);
        }

        MatchValue(TokenKind.SEPARATOR, ";");
        return new ImportNode { Path = path.ToString() };
    }

    private ClassDeclNode ParseClass()
    {
        MatchValue(TokenKind.KEYWORD, "class");
        var name = Match(TokenKind.IDENTIFIER).Value;
        MatchValue(TokenKind.SEPARATOR, "{");

        var node = new ClassDeclNode { Name = name };

        while (!Check(TokenKind.SEPARATOR, "}"))
        {
            if (IsEOF)
            {
                Report(new SyntaxException("Unterminated class body", Current));
                return node;
            }

            try
            {
                node.Members.Add(ParseClassMember());
            }
            catch (SyntaxException ex)
            {
                Report(ex);
                SynchronizeToClassMemberBoundary();
            }
        }

        MatchValue(TokenKind.SEPARATOR, "}");
        return node;
    }

    private AstNode ParseClassMember()
    {
        while (Check(TokenKind.KEYWORD, "public") || Check(TokenKind.KEYWORD, "static"))
            Advance();

        var type = ReadTypeName(ref _pos);
        var name = Match(TokenKind.IDENTIFIER).Value;

        if (Check(TokenKind.SEPARATOR, "("))
        {
            Advance();
            var method = new MethodDeclNode { ReturnType = type, Name = name };

            while (!Check(TokenKind.SEPARATOR, ")"))
            {
                var paramType = ReadTypeName(ref _pos);
                var paramName = Match(TokenKind.IDENTIFIER).Value;
                method.Parameters.Add(new ParameterNode { Type = paramType, Name = paramName });

                if (Check(TokenKind.SEPARATOR, ","))
                    Advance();
                else if (!Check(TokenKind.SEPARATOR, ")"))
                    throw new SyntaxException("Expected ',' or ')'", Current);
            }

            MatchValue(TokenKind.SEPARATOR, ")");
            method.Body = ParseBlock();
            return method;
        }

        ExpressionNode? init = null;
        if (Check(TokenKind.OPERATOR, "="))
        {
            Advance();
            init = ParseExpression();
        }

        MatchValue(TokenKind.SEPARATOR, ";");
        return new VariableDeclNode { Type = type, Name = name, Initializer = init };
    }

    private BlockStatementNode ParseBlock()
    {
        MatchValue(TokenKind.SEPARATOR, "{");
        var block = new BlockStatementNode();

        while (!Check(TokenKind.SEPARATOR, "}"))
        {
            if (IsEOF)
            {
                Report(new SyntaxException("Unterminated block", Current));
                return block;
            }

            try
            {
                block.Statements.Add(ParseStatement());
            }
            catch (SyntaxException ex)
            {
                Report(ex);
                SynchronizeToStatementBoundary();
            }
        }

        MatchValue(TokenKind.SEPARATOR, "}");
        return block;
    }

    private StatementNode ParseStatement()
    {
        if (Check(TokenKind.SEPARATOR, "{")) return ParseBlock();
        if (Check(TokenKind.KEYWORD, "if")) return ParseIf();
        if (Check(TokenKind.KEYWORD, "switch")) return ParseSwitchStatement();
        if (Check(TokenKind.KEYWORD, "while")) return ParseWhile();
        if (Check(TokenKind.KEYWORD, "do")) return ParseDoWhile();
        if (Check(TokenKind.KEYWORD, "for")) return ParseFor();
        if (Check(TokenKind.KEYWORD, "return")) return ParseReturn();
        if (Check(TokenKind.KEYWORD, "break")) { Advance(); MatchValue(TokenKind.SEPARATOR, ";"); return new BreakStatementNode(); }
        if (Check(TokenKind.KEYWORD, "continue")) { Advance(); MatchValue(TokenKind.SEPARATOR, ";"); return new ContinueStatementNode(); }
        if (IsVarDeclStart()) return ParseVarDeclStmt();

        var expr = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ";");
        return new ExpressionStatementNode { Expression = expr };
    }

    private IfStatementNode ParseIf()
    {
        Advance();
        MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");

        var thenBranch = ParseStatement();
        StatementNode? elseBranch = null;

        if (Check(TokenKind.KEYWORD, "else"))
        {
            Advance();
            elseBranch = ParseStatement();
        }

        return new IfStatementNode
        {
            Condition = cond,
            ThenBranch = thenBranch,
            ElseBranch = elseBranch
        };
    }

    private StatementNode ParseSwitchStatement()
    {
        ParseSwitchHeader();
        ParseSwitchSections();
        MatchValue(TokenKind.SEPARATOR, "}");
        return new BlockStatementNode();
    }

    private WhileStatementNode ParseWhile()
    {
        Advance();
        MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        return new WhileStatementNode { Condition = cond, Body = ParseStatement() };
    }

    private DoWhileStatementNode ParseDoWhile()
    {
        Advance();
        var body = ParseStatement();
        MatchValue(TokenKind.KEYWORD, "while");
        MatchValue(TokenKind.SEPARATOR, "(");
        var cond = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        MatchValue(TokenKind.SEPARATOR, ";");
        return new DoWhileStatementNode { Body = body, Condition = cond };
    }

    private StatementNode ParseFor()
    {
        Advance();
        MatchValue(TokenKind.SEPARATOR, "(");

        AstNode? init = null;
        ExpressionNode? condition = null;
        ExpressionNode? increment = null;

        if (!Check(TokenKind.SEPARATOR, ";"))
        {
            if (IsVarDeclStart())
            {
                var type = ReadTypeName(ref _pos);
                var name = Match(TokenKind.IDENTIFIER).Value;
                var decl = new VariableDeclNode { Type = type, Name = name };

                if (Check(TokenKind.OPERATOR, "="))
                {
                    Advance();
                    decl.Initializer = ParseExpression();
                }

                if (Check(TokenKind.OPERATOR, ":"))
                {
                    Advance();
                    init = decl;
                    condition = ParseExpression();
                    MatchValue(TokenKind.SEPARATOR, ")");
                    return new ForStatementNode
                    {
                        Initialization = init,
                        Condition = condition,
                        Increment = null,
                        Body = ParseStatement()
                    };
                }

                init = decl;
            }
            else
            {
                init = ParseExpression();
            }
        }

        MatchValue(TokenKind.SEPARATOR, ";");

        if (!Check(TokenKind.SEPARATOR, ";"))
            condition = ParseExpression();

        MatchValue(TokenKind.SEPARATOR, ";");

        if (!Check(TokenKind.SEPARATOR, ")"))
            increment = ParseExpression();

        MatchValue(TokenKind.SEPARATOR, ")");

        return new ForStatementNode
        {
            Initialization = init,
            Condition = condition,
            Increment = increment,
            Body = ParseStatement()
        };
    }

    private ReturnStatementNode ParseReturn()
    {
        Advance();
        ExpressionNode? val = !Check(TokenKind.SEPARATOR, ";") ? ParseExpression() : null;
        MatchValue(TokenKind.SEPARATOR, ";");
        return new ReturnStatementNode { Value = val };
    }

    private bool IsVarDeclStart()
    {
        if (IsEOF) return false;
        if (!IsTypeStartToken(Current)) return false;

        int index = _pos;
        try
        {
            ReadTypeName(ref index);
        }
        catch
        {
            return false;
        }

        return CheckAt(index, TokenKind.IDENTIFIER);
    }

    private VariableDeclNode ParseVarDeclStmt(bool semi = true)
    {
        var type = ReadTypeName(ref _pos);
        var name = Match(TokenKind.IDENTIFIER).Value;
        ExpressionNode? init = null;

        if (Check(TokenKind.OPERATOR, "="))
        {
            Advance();
            init = ParseExpression();
        }

        if (semi) MatchValue(TokenKind.SEPARATOR, ";");
        return new VariableDeclNode { Type = type, Name = name, Initializer = init };
    }

    private ExpressionNode ParseExpression() => ParseAssignment();

    private ExpressionNode ParseAssignment()
    {
        var expr = ParseTernary();

        if (Check(TokenKind.OPERATOR) && IsAssignmentOperator(Current.Value))
        {
            var op = Advance().Value;
            return new AssignmentExpressionNode
            {
                Target = expr,
                Operator = op,
                Value = ParseAssignment()
            };
        }

        return expr;
    }

    private ExpressionNode ParseTernary()
    {
        var expr = ParseBinary(0);

        if (Check(TokenKind.OPERATOR, "?"))
        {
            Advance();
            var thenExpr = ParseExpression();
            MatchValue(TokenKind.OPERATOR, ":");
            var elseExpr = ParseExpression();

            return new BinaryExpressionNode
            {
                Left = expr,
                Operator = "?",
                Right = new BinaryExpressionNode
                {
                    Left = thenExpr,
                    Operator = ":",
                    Right = elseExpr
                }
            };
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
            expr = new BinaryExpressionNode
            {
                Left = expr,
                Operator = op,
                Right = ParseBinary(nextPrec)
            };
        }

        return expr;
    }

    private int GetPrec(string op) => op switch
    {
        "||" => 1,
        "&&" => 2,
        "==" or "!=" or ">=" or "<=" or ">" or "<" => 3,
        "+" or "-" => 4,
        "*" or "/" or "%" => 5,
        _ => 0
    };

    private ExpressionNode ParseUnary()
    {
        if (Check(TokenKind.OPERATOR) && (Current.Value == "!" || Current.Value == "-" || Current.Value == "++" || Current.Value == "--"))
            return new UnaryExpressionNode
            {
                Operator = Advance().Value,
                Operand = ParseUnary(),
                IsPostfix = false
            };

        if (Check(TokenKind.KEYWORD, "throw"))
        {
            Advance();
            return new UnaryExpressionNode { Operator = "throw", Operand = ParseExpression() };
        }

        return ParsePostfix();
    }

    private ExpressionNode ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Check(TokenKind.SEPARATOR, "."))
            {
                Advance();
                expr = new MemberAccessExpressionNode
                {
                    Target = expr,
                    MemberName = Match(TokenKind.IDENTIFIER).Value
                };
            }
            else if (Check(TokenKind.SEPARATOR, "("))
            {
                Advance();
                var call = new MethodCallExpressionNode { Target = expr };

                if (!Check(TokenKind.SEPARATOR, ")"))
                {
                    do
                    {
                        call.Arguments.Add(ParseExpression());
                    }
                    while (Check(TokenKind.SEPARATOR, ",") && Advance() != null);
                }

                MatchValue(TokenKind.SEPARATOR, ")");
                expr = call;
            }
            else if (Check(TokenKind.SEPARATOR, "["))
            {
                Advance();
                var index = ParseExpression();
                MatchValue(TokenKind.SEPARATOR, "]");
                expr = new BinaryExpressionNode
                {
                    Left = expr,
                    Operator = "[]",
                    Right = index
                };
            }
            else if (Check(TokenKind.OPERATOR, "++") || Check(TokenKind.OPERATOR, "--"))
            {
                expr = new UnaryExpressionNode
                {
                    Operator = Advance().Value,
                    Operand = expr,
                    IsPostfix = true
                };
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private ExpressionNode ParsePrimary()
    {
        if (Check(TokenKind.LITERAL))
            return new LiteralExpressionNode { Value = Advance().Value };

        if (Check(TokenKind.IDENTIFIER))
            return new IdentifierExpressionNode { Name = Advance().Value };

        if (Check(TokenKind.SEPARATOR, "("))
        {
            Advance();
            var e = ParseExpression();
            MatchValue(TokenKind.SEPARATOR, ")");
            return e;
        }

        if (Check(TokenKind.KEYWORD, "new"))
            return ParseObjectCreation();

        if (Check(TokenKind.KEYWORD, "switch"))
            return ParseSwitchExpression();

        throw new SyntaxException($"Unexpected token {Current.Value}", Current);
    }

    private ExpressionNode ParseObjectCreation()
    {
        Advance();
        var type = ReadTypeName(ref _pos, allowArraySuffix: false);

        if (Check(TokenKind.SEPARATOR, "("))
        {
            Advance();
            var obj = new ObjectCreationExpressionNode { Type = type };

            if (!Check(TokenKind.SEPARATOR, ")"))
            {
                do
                {
                    obj.Arguments.Add(ParseExpression());
                }
                while (Check(TokenKind.SEPARATOR, ",") && Advance() != null);
            }

            MatchValue(TokenKind.SEPARATOR, ")");
            return obj;
        }

        var array = new ObjectCreationExpressionNode { Type = type + "[]" };

        while (Check(TokenKind.SEPARATOR, "["))
        {
            Advance();

            if (!Check(TokenKind.SEPARATOR, "]"))
                array.Arguments.Add(ParseExpression());

            MatchValue(TokenKind.SEPARATOR, "]");

            if (Check(TokenKind.SEPARATOR, "["))
                array.Type += "[]";
        }

        return array;
    }

    private ExpressionNode ParseSwitchExpression()
    {
        ParseSwitchHeader();
        ParseSwitchSections();
        MatchValue(TokenKind.SEPARATOR, "}");
        return new IdentifierExpressionNode { Name = "switch" };
    }

    private void ParseSwitchHeader()
    {
        MatchValue(TokenKind.KEYWORD, "switch");
        MatchValue(TokenKind.SEPARATOR, "(");
        _ = ParseExpression();
        MatchValue(TokenKind.SEPARATOR, ")");
        MatchValue(TokenKind.SEPARATOR, "{");
    }

    private void ParseSwitchSections()
    {
        bool sawDefault = false;

        while (!Check(TokenKind.SEPARATOR, "}"))
        {
            if (IsEOF)
            {
                Report(new SyntaxException("Unterminated switch block", Current));
                return;
            }

            try
            {
                if (Check(TokenKind.KEYWORD, "case"))
                {
                    ParseSwitchCaseSection();
                    continue;
                }

                if (Check(TokenKind.KEYWORD, "default"))
                {
                    if (sawDefault)
                        throw new SyntaxException("Duplicate 'default' label", Current);

                    sawDefault = true;
                    ParseSwitchDefaultSection();
                    continue;
                }

                throw new SyntaxException("Expected 'case' or 'default' in switch block", Current);
            }
            catch (SyntaxException ex)
            {
                Report(ex);
                SynchronizeToSwitchSectionBoundary();
            }
        }
    }

    private void ParseSwitchCaseSection()
    {
        MatchValue(TokenKind.KEYWORD, "case");
        ParseSwitchLabelList();
        ParseSwitchArmDelimiterAndBody();
    }

    private void ParseSwitchDefaultSection()
    {
        MatchValue(TokenKind.KEYWORD, "default");
        ParseSwitchArmDelimiterAndBody();
    }

    private void ParseSwitchLabelList()
    {
        _ = ParseExpression();

        while (Check(TokenKind.SEPARATOR, ","))
        {
            Advance();
            _ = ParseExpression();
        }
    }

    private void ParseSwitchArmDelimiterAndBody()
    {
        if (Check(TokenKind.OPERATOR, ":"))
        {
            Advance();
            ParseSwitchColonBody();
            return;
        }

        if (Check(TokenKind.OPERATOR, "->"))
        {
            Advance();
            ParseSwitchArrowBody();
            return;
        }

        throw new SyntaxException("Expected ':' or '->' after switch label", Current);
    }

    private void ParseSwitchColonBody()
    {
        while (!Check(TokenKind.KEYWORD, "case") &&
               !Check(TokenKind.KEYWORD, "default") &&
               !Check(TokenKind.SEPARATOR, "}"))
        {
            if (IsEOF)
                throw new SyntaxException("Unterminated switch block", Current);

            ParseStatement();
        }
    }

    private void ParseSwitchArrowBody()
    {
        if (Check(TokenKind.SEPARATOR, "{"))
        {
            ParseBlock();
            if (Check(TokenKind.SEPARATOR, ";"))
                Advance();
            return;
        }

        _ = ParseExpression();

        if (Check(TokenKind.SEPARATOR, ";"))
            Advance();
    }

    private string ReadTypeName(ref int index, bool allowArraySuffix = true)
    {
        if (!IsTypeStartToken(PeekAt(index)))
            throw new SyntaxException("Expected type name", PeekAt(index));

        var sb = new StringBuilder();
        sb.Append(AdvanceAt(ref index).Value);

        while (CheckAt(index, TokenKind.SEPARATOR, "."))
        {
            index++;
            sb.Append('.');

            if (!IsTypeNameToken(PeekAt(index)))
                throw new SyntaxException("Expected type name", PeekAt(index));

            sb.Append(AdvanceAt(ref index).Value);
        }

        if (CheckAt(index, TokenKind.OPERATOR, "<"))
        {
            int depth = 0;

            while (true)
            {
                var token = AdvanceAt(ref index);
                sb.Append(token.Value);

                if (token.Kind == TokenKind.OPERATOR)
                {
                    if (token.Value == "<") depth++;
                    else if (token.Value == ">")
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                }

                if (token.Kind == TokenKind.EOF)
                    throw new SyntaxException("Unterminated generic type", token);
            }
        }

        if (allowArraySuffix)
        {
            while (CheckAt(index, TokenKind.SEPARATOR, "["))
            {
                index++;
                if (!CheckAt(index, TokenKind.SEPARATOR, "]"))
                    throw new SyntaxException("Expected ']'", PeekAt(index));
                index++;
                sb.Append("[]");
            }
        }

        return sb.ToString();
    }

    private bool IsTypeStartToken(Token token)
    {
        if (token.Kind == TokenKind.IDENTIFIER) return true;
        if (token.Kind != TokenKind.KEYWORD) return false;
        return IsTypeKeyword(token.Value);
    }

    private bool IsTypeNameToken(Token token)
    {
        return token.Kind == TokenKind.IDENTIFIER || (token.Kind == TokenKind.KEYWORD && IsTypeKeyword(token.Value));
    }

    private bool IsTypeKeyword(string value) => value is
        "int" or "long" or "double" or "float" or "char" or "boolean" or "byte" or "short" or "void" or "var" or "String" or "Scanner";

    private bool IsAssignmentOperator(string op) => op is
        "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "&=" or "|=" or "^=" or "<<=" or ">>=";

    private bool IsStatementStart(Token token)
    {
        if (token.Kind == TokenKind.IDENTIFIER || token.Kind == TokenKind.LITERAL)
            return true;

        if (token.Kind == TokenKind.SEPARATOR)
            return token.Value is "{" or "(" or "[" or "}";

        if (token.Kind != TokenKind.KEYWORD)
            return false;

        return token.Value is
            "if" or "switch" or "while" or "do" or "for" or "return" or
            "break" or "continue" or "new" or "throw" or "case" or "default" or
            "public" or "static" or "class" or "import";
    }

    private void SynchronizeToStatementBoundary()
    {
        while (!IsEOF)
        {
            if (Check(TokenKind.SEPARATOR, ";"))
            {
                Advance();
                return;
            }

            if (Check(TokenKind.SEPARATOR, "}") || Check(TokenKind.KEYWORD, "case") || Check(TokenKind.KEYWORD, "default") || IsStatementStart(Current))
                return;

            Advance();
        }
    }

    private void SynchronizeToSwitchSectionBoundary()
    {
        while (!IsEOF)
        {
            if (Check(TokenKind.SEPARATOR, "}") || Check(TokenKind.KEYWORD, "case") || Check(TokenKind.KEYWORD, "default"))
                return;

            Advance();
        }
    }

    private void SynchronizeToClassMemberBoundary()
    {
        while (!IsEOF)
        {
            if (Check(TokenKind.SEPARATOR, "}") ||
                Check(TokenKind.KEYWORD, "public") ||
                Check(TokenKind.KEYWORD, "static") ||
                Check(TokenKind.KEYWORD, "class") ||
                Check(TokenKind.KEYWORD, "import"))
                return;

            if (Check(TokenKind.SEPARATOR, ";"))
            {
                Advance();
                return;
            }

            Advance();
        }
    }

    private void SynchronizeToTopLevelBoundary()
    {
        while (!IsEOF)
        {
            if (Check(TokenKind.KEYWORD, "import") || Check(TokenKind.KEYWORD, "class"))
                return;

            if (Check(TokenKind.KEYWORD, "public") || Check(TokenKind.KEYWORD, "static"))
                return;

            if (Check(TokenKind.SEPARATOR, ";") || Check(TokenKind.SEPARATOR, "}"))
            {
                Advance();
                return;
            }

            Advance();
        }
    }
}