using System.Globalization;
using JavaTranslator.Ast;

namespace JavaTranslator;

public class SemanticAnalyzer
{
    private readonly Stack<Dictionary<string, string>> _scopes = new();
    private readonly Dictionary<string, string> _methods = new(StringComparer.Ordinal);
    public List<string> Errors { get; } = new();

    private static readonly Dictionary<string, int> NumericRank = new()
    {
        ["byte"] = 1,
        ["short"] = 2,
        ["char"] = 2,
        ["int"] = 3,
        ["long"] = 4,
        ["float"] = 5,
        ["double"] = 6
    };

    private static readonly HashSet<string> PseudoKeywords = new(StringComparer.Ordinal)
    {
        "switch", "case", "default", "throw", "break", "continue"
    };

    public void Analyze(CompilationUnitNode root)
    {
        EnterScope();

        foreach (var cls in root.Classes)
            RegisterMethods(cls);

        foreach (var cls in root.Classes)
            VisitClass(cls);

        ExitScope();
    }

    private void RegisterMethods(ClassDeclNode cls)
    {
        foreach (var member in cls.Members)
        {
            if (member is MethodDeclNode m)
                _methods[m.Name] = m.ReturnType;
        }
    }

    private void VisitClass(ClassDeclNode cls)
    {
        foreach (var member in cls.Members)
        {
            if (member is MethodDeclNode m)
                VisitMethod(m);
        }
    }

    private void VisitMethod(MethodDeclNode method)
    {
        EnterScope();

        foreach (var p in method.Parameters)
            Declare(p.Name, p.Type);

        VisitBlock(method.Body);

        ExitScope();
    }

    private void VisitBlock(BlockStatementNode block)
    {
        EnterScope();

        foreach (var stmt in block.Statements)
            VisitStatement(stmt);

        ExitScope();
    }

    private void VisitStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case VariableDeclNode v:
                VisitVarDecl(v);
                break;

            case ExpressionStatementNode e:
                EvaluateExpression(e.Expression);
                break;

            case IfStatementNode i:
                RequireBoolean(EvaluateExpression(i.Condition), "if");
                VisitAny(i.ThenBranch);
                if (i.ElseBranch != null)
                    VisitAny(i.ElseBranch);
                break;

            case WhileStatementNode w:
                RequireBoolean(EvaluateExpression(w.Condition), "while");
                VisitAny(w.Body);
                break;

            case DoWhileStatementNode d:
                VisitAny(d.Body);
                RequireBoolean(EvaluateExpression(d.Condition), "do-while");
                break;

            case ForStatementNode f:
                if (IsEnhancedFor(f))
                {
                    EnterScope();

                    if (f.Initialization is VariableDeclNode vd)
                    {
                        var iterableType = f.Condition != null ? EvaluateExpression(f.Condition) : "unknown";
                        var elementType = GetIterableElementType(iterableType);

                        if (vd.Type == "var")
                        {
                            if (elementType == "unknown")
                            {
                                Error($"Cannot infer type for '{vd.Name}'");
                                Declare(vd.Name, "unknown");
                            }
                            else
                            {
                                Declare(vd.Name, elementType);
                            }
                        }
                        else
                        {
                            if (elementType != "unknown" && !IsAssignable(vd.Type, elementType))
                                Error($"Cannot assign '{elementType}' to '{vd.Type}'");

                            Declare(vd.Name, vd.Type);
                        }
                    }
                    else if (f.Initialization != null)
                    {
                        VisitAny(f.Initialization);
                    }

                    VisitAny(f.Body);
                    ExitScope();
                    break;
                }

                EnterScope();

                if (f.Initialization != null)
                    VisitAny(f.Initialization);

                if (f.Condition != null)
                    RequireBoolean(EvaluateExpression(f.Condition), "for");

                if (f.Increment != null)
                    VisitAny(f.Increment);

                VisitAny(f.Body);

                ExitScope();
                break;

            case BlockStatementNode b:
                VisitBlock(b);
                break;
        }
    }

    private bool IsEnhancedFor(ForStatementNode f) =>
        f.Initialization is VariableDeclNode vd &&
        vd.Initializer == null &&
        f.Increment == null &&
        f.Condition != null &&
        f.Condition is not null;

    private void VisitAny(AstNode node)
    {
        switch (node)
        {
            case StatementNode s:
                VisitStatement(s);
                break;

            case ExpressionNode e:
                EvaluateExpression(e);
                break;
        }
    }

    private void VisitVarDecl(VariableDeclNode v)
    {
        if (IsDeclaredInCurrentScope(v.Name))
        {
            Error($"Variable '{v.Name}' already declared");
            return;
        }

        string declaredType = v.Type;

        if (v.Initializer != null)
        {
            var exprType = EvaluateExpression(v.Initializer);

            if (declaredType == "var")
            {
                if (exprType == "unknown" || exprType == "null")
                {
                    Error($"Cannot infer type for '{v.Name}'");
                    declaredType = "unknown";
                }
                else
                {
                    declaredType = exprType;
                }
            }
            else if (!IsAssignable(declaredType, exprType, v.Initializer))
            {
                Error($"Cannot assign '{exprType}' to '{declaredType}'");
            }
        }

        Declare(v.Name, declaredType);
    }

    private string EvaluateExpression(ExpressionNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionNode l:
                return InferLiteralType(l.Value);

            case IdentifierExpressionNode id:
                if (PseudoKeywords.Contains(id.Name))
                    return "unknown";

                return Resolve(id.Name);

            case AssignmentExpressionNode a:
            {
                var leftType = EvaluateExpression(a.Target);
                var rightType = EvaluateExpression(a.Value);

                if (a.Operator == "+=")
                {
                    if (leftType != "unknown" && rightType != "unknown")
                    {
                        if (leftType == "String")
                            return "String";

                        if (IsNumeric(leftType) && (IsNumeric(rightType) || rightType == "String"))
                            return leftType;

                        Error($"Cannot apply '+=' between '{leftType}' and '{rightType}'");
                    }

                    return leftType;
                }

                if (!IsAssignable(leftType, rightType, a.Value))
                    Error($"Cannot assign '{rightType}' to '{leftType}'");

                return leftType;
            }

            case BinaryExpressionNode b:
            {
                var lt = EvaluateExpression(b.Left);
                var rt = EvaluateExpression(b.Right);
                return CheckBinary(b.Operator, lt, rt);
            }

            case UnaryExpressionNode u:
                return EvaluateExpression(u.Operand);

            case ObjectCreationExpressionNode o:
                return o.Type;

            case MethodCallExpressionNode m:
            {
                if (m.Target is IdentifierExpressionNode fid)
                {
                    foreach (var arg in m.Arguments)
                        EvaluateExpression(arg);

                    if (_methods.TryGetValue(fid.Name, out var returnType))
                        return returnType;

                    return "unknown";
                }

                if (m.Target is MemberAccessExpressionNode outer &&
                    (outer.MemberName is "println" or "print") &&
                    outer.Target is MemberAccessExpressionNode inner &&
                    inner.MemberName == "out" &&
                    inner.Target is IdentifierExpressionNode sys &&
                    sys.Name == "System")
                {
                    foreach (var arg in m.Arguments)
                        EvaluateExpression(arg);

                    return "void";
                }

                var targetType = EvaluateExpression(m.Target);

                foreach (var arg in m.Arguments)
                    EvaluateExpression(arg);

                if (m.Target is MemberAccessExpressionNode ma)
                {
                    if (ma.MemberName is "println" or "print")
                        return "void";

                    if (ma.MemberName == "nextInt")
                        return "int";

                    if (ma.MemberName is "nextLine" or "next")
                        return "String";

                    if (ma.MemberName == "isEmpty")
                        return "boolean";

                    if (ma.MemberName == "add")
                        return "boolean";

                    if (ma.MemberName == "put")
                        return "unknown";

                    if (ma.MemberName == "entrySet")
                        return "unknown";

                    if (ma.MemberName is "getKey" or "getValue")
                        return "unknown";
                }

                return "unknown";
            }

            case MemberAccessExpressionNode ma:
            {
                if (ma.Target is IdentifierExpressionNode id && id.Name == "System")
                {
                    if (ma.MemberName == "out")
                        return "PrintStream";

                    if (ma.MemberName == "in")
                        return "InputStream";

                    return "unknown";
                }

                var targetType = EvaluateExpression(ma.Target);

                if (ma.MemberName == "length" && IsArrayType(targetType))
                    return "int";

                if (ma.MemberName == "length" && targetType == "String")
                    return "int";

                return ma.MemberName switch
                {
                    "out" => "PrintStream",
                    "in" => "InputStream",
                    _ => "unknown"
                };
            }

            default:
                return "unknown";
        }
    }

    private string CheckBinary(string op, string l, string r)
    {
        if (l == "unknown" || r == "unknown")
        {
            if (op is "?" or ":" or "->" or "[]")
                return MergeTypes(l, r);

            return "unknown";
        }

        if (op == "[]")
        {
            if (IsArrayType(l))
                return GetArrayElementType(l);

            Error($"Invalid indexing on '{l}'");
            return "unknown";
        }

        if (op == "+")
        {
            if (l == "String" || r == "String")
                return "String";

            if (IsNumeric(l) && IsNumeric(r))
                return Promote(l, r);

            Error($"Invalid '+' between '{l}' and '{r}'");
            return "unknown";
        }

        if (op is "-" or "*" or "/" or "%")
        {
            if (IsNumeric(l) && IsNumeric(r))
                return Promote(l, r);

            Error($"Invalid '{op}' between '{l}' and '{r}'");
            return "unknown";
        }

        if (op is "==" or "!=")
            return "boolean";

        if (op is ">" or "<" or ">=" or "<=")
        {
            if (IsNumeric(l) && IsNumeric(r))
                return "boolean";

            Error($"Invalid comparison '{op}' between '{l}' and '{r}'");
            return "unknown";
        }

        if (op == "?")
        {
            if (l != "boolean" && l != "unknown")
                Error($"Expected boolean in ternary, got '{l}'");

            return r;
        }

        if (op == ":")
            return MergeTypes(l, r);

        if (op == "->")
            return r;

        Error($"Unsupported operator '{op}'");
        return "unknown";
    }

    private string MergeTypes(string a, string b)
    {
        if (a == "unknown") return b;
        if (b == "unknown") return a;
        if (a == "null") return b;
        if (b == "null") return a;
        if (a == b) return a;

        if (a == "String" || b == "String")
            return "String";

        if (IsNumeric(a) && IsNumeric(b))
            return Promote(a, b);

        if (IsArrayType(a) && IsArrayType(b))
            return a == b ? a : "unknown";

        return "unknown";
    }

    private string Promote(string a, string b)
    {
        if (!IsNumeric(a) || !IsNumeric(b))
            return "unknown";

        return NumericRank[a] >= NumericRank[b] ? a : b;
    }

    private bool IsNumeric(string t) =>
        NumericRank.ContainsKey(t);

    private bool IsArrayType(string t) =>
        t.EndsWith("[]", StringComparison.Ordinal);

    private string GetArrayElementType(string t) =>
        IsArrayType(t) ? t[..^2] : "unknown";

    private string GetIterableElementType(string iterableType)
    {
        if (iterableType == "unknown")
            return "unknown";

        if (IsArrayType(iterableType))
            return GetArrayElementType(iterableType);

        return "unknown";
    }

    private string InferLiteralType(string val)
    {
        val = val.Trim();

        if (val == "null") return "null";
        if (val.StartsWith("\"")) return "String";
        if (val.StartsWith("'")) return "char";
        if (val == "true" || val == "false") return "boolean";

        if (val.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            return "float";

        if (val.EndsWith("l", StringComparison.OrdinalIgnoreCase))
            return "long";

        if (val.Contains('.') || val.Contains('e') || val.Contains('E') || val.Contains('p') || val.Contains('P'))
            return "double";

        return "int";
    }

    private void RequireBoolean(string type, string context)
    {
        if (type == "unknown") return;

        if (type != "boolean")
            Error($"Expected boolean in {context}, got '{type}'");
    }

    private bool IsAssignable(string target, string source, ExpressionNode? sourceExpr = null)
    {
        if (source == "unknown" || target == "unknown")
            return true;

        if (source == "null")
            return !IsNumeric(target) && target != "boolean" && target != "void";

        if (target == source)
            return true;

        if (IsNumeric(target) && IsNumeric(source))
        {
            if (NumericRank[source] <= NumericRank[target])
                return true;

            if (sourceExpr is LiteralExpressionNode lit &&
                TryGetIntegralLiteralValue(lit.Value, out var value) &&
                IsNarrowingAllowedByConstant(target, source, value))
            {
                return true;
            }

            return false;
        }

        if (IsArrayType(target) && IsArrayType(source))
            return GetArrayElementType(target) == GetArrayElementType(source);

        return false;
    }

    private bool IsNarrowingAllowedByConstant(string target, string source, long value)
    {
        if (source is not ("byte" or "short" or "char" or "int" or "long"))
            return false;

        return target switch
        {
            "byte" => value >= sbyte.MinValue && value <= sbyte.MaxValue,
            "short" => value >= short.MinValue && value <= short.MaxValue,
            "char" => value >= char.MinValue && value <= char.MaxValue,
            "int" => value >= int.MinValue && value <= int.MaxValue,
            "long" => true,
            _ => false
        };
    }

    private bool TryGetIntegralLiteralValue(string literal, out long value)
    {
        value = 0;

        var s = literal.Trim().Replace("_", "");

        if (s.Length == 0)
            return false;

        if (s.EndsWith("L", StringComparison.OrdinalIgnoreCase))
            s = s[..^1];

        bool negative = false;
        if (s[0] is '+' or '-')
        {
            negative = s[0] == '-';
            s = s[1..];
        }

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(s[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
                return false;
        }
        else if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                value = Convert.ToInt64(s[2..], 2);
            }
            catch
            {
                return false;
            }
        }
        else if (s.Length > 1 && s[0] == '0' && s.All(ch => ch is >= '0' and <= '7'))
        {
            try
            {
                value = Convert.ToInt64(s[1..], 8);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return false;
        }

        if (negative)
            value = -value;

        return true;
    }

    private void Declare(string name, string type)
    {
        _scopes.Peek()[name] = type;
    }

    private string Resolve(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var t))
                return t;
        }

        if (name == "System")
            return "System";

        if (_methods.TryGetValue(name, out var methodType))
            return methodType;

        if (PseudoKeywords.Contains(name))
            return "unknown";

        Error($"Undeclared variable '{name}'");
        return "unknown";
    }

    private bool IsDeclaredInCurrentScope(string name) =>
        _scopes.Peek().ContainsKey(name);

    private void EnterScope() =>
        _scopes.Push(new Dictionary<string, string>());

    private void ExitScope() =>
        _scopes.Pop();

    private void Error(string msg)
    {
        if (!Errors.Contains($"[SEMANTIC] {msg}"))
            Errors.Add($"[SEMANTIC] {msg}");
    }
}
