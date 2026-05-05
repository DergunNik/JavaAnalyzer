using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JavaTranslator.Ast;

namespace JavaTranslator;

public sealed class Interpreter
{
    private readonly Dictionary<string, MethodDeclNode> _methods = new(StringComparer.Ordinal);
    private readonly Stack<Dictionary<string, object?>> _scopes = new();

    public void Execute(CompilationUnitNode root)
    {
        RegisterMethods(root);

        var main = FindMainMethod();
        if (main == null)
            throw new InvalidOperationException("Main method was not found.");

        InvokeMethod(main, Array.Empty<object?>());
    }

    private void RegisterMethods(CompilationUnitNode root)
    {
        foreach (var cls in root.Classes)
        {
            foreach (var member in cls.Members)
            {
                if (member is MethodDeclNode m)
                    _methods[m.Name] = m;
            }
        }
    }

    private MethodDeclNode? FindMainMethod()
    {
        return _methods.TryGetValue("main", out var main) ? main : null;
    }

    private object? InvokeMethod(MethodDeclNode method, IReadOnlyList<object?> args)
    {
        EnterScope();

        for (int i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var value = i < args.Count ? args[i] : null;
            Declare(param.Name, value);
        }

        try
        {
            ExecuteBlock(method.Body);
        }
        catch (ReturnSignal r)
        {
            ExitScope();
            return r.Value;
        }

        ExitScope();
        return null;
    }

    private void ExecuteBlock(BlockStatementNode block)
    {
        EnterScope();

        try
        {
            foreach (var stmt in block.Statements)
                ExecuteStatement(stmt);
        }
        finally
        {
            ExitScope();
        }
    }

    private void ExecuteStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case VariableDeclNode v:
                ExecuteVarDecl(v);
                break;

            case ExpressionStatementNode e:
                EvaluateExpression(e.Expression);
                break;

            case BlockStatementNode b:
                ExecuteBlock(b);
                break;

            case IfStatementNode i:
                if (ToBool(EvaluateExpression(i.Condition)))
                {
                    ExecuteAny(i.ThenBranch);
                }
                else if (i.ElseBranch != null)
                {
                    ExecuteAny(i.ElseBranch);
                }
                break;

            case WhileStatementNode w:
                while (ToBool(EvaluateExpression(w.Condition)))
                {
                    try
                    {
                        ExecuteAny(w.Body);
                    }
                    catch (ContinueSignal)
                    {
                        continue;
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                }
                break;

            case DoWhileStatementNode d:
                do
                {
                    try
                    {
                        ExecuteAny(d.Body);
                    }
                    catch (ContinueSignal)
                    {
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                }
                while (ToBool(EvaluateExpression(d.Condition)));
                break;

            case ForStatementNode f:
                ExecuteFor(f);
                break;

            case ReturnStatementNode r:
                throw new ReturnSignal(r.Value != null ? EvaluateExpression(r.Value) : null);

            case BreakStatementNode:
                throw new BreakSignal();

            case ContinueStatementNode:
                throw new ContinueSignal();
        }
    }

    private void ExecuteAny(AstNode node)
    {
        if (node is StatementNode s)
            ExecuteStatement(s);
        else if (node is ExpressionNode e)
            EvaluateExpression(e);
    }

    private void ExecuteFor(ForStatementNode f)
    {
        EnterScope();

        try
        {
            if (IsEnhancedFor(f))
            {
                var decl = (VariableDeclNode)f.Initialization!;
                var iterable = f.Condition != null ? EvaluateExpression(f.Condition) : null;

                foreach (var item in Enumerate(iterable))
                {
                    Declare(decl.Name, item);

                    try
                    {
                        ExecuteAny(f.Body);
                    }
                    catch (ContinueSignal)
                    {
                        continue;
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                }

                return;
            }

            if (f.Initialization != null)
                ExecuteAny(f.Initialization);

            while (f.Condition == null || ToBool(EvaluateExpression(f.Condition)))
            {
                try
                {
                    ExecuteAny(f.Body);
                }
                catch (ContinueSignal)
                {
                }
                catch (BreakSignal)
                {
                    break;
                }

                if (f.Increment != null)
                    _ = EvaluateExpression(f.Increment);
            }
        }
        finally
        {
            ExitScope();
        }
    }

    private bool IsEnhancedFor(ForStatementNode f)
    {
        return f.Initialization is VariableDeclNode vd
               && vd.Initializer == null
               && f.Condition != null
               && f.Increment == null;
    }

    private void ExecuteVarDecl(VariableDeclNode v)
    {
        if (IsDeclaredInCurrentScope(v.Name))
            throw new InvalidOperationException($"Variable '{v.Name}' already declared in current scope.");

        object? value = null;

        if (v.Initializer != null)
            value = EvaluateExpression(v.Initializer);

        if (v.Type == "var" && value == null)
            throw new InvalidOperationException($"Cannot infer type for variable '{v.Name}'.");

        if (v.Type.EndsWith("[]", StringComparison.Ordinal) && value is null)
            value = CreateArray(v.Type, null);

        Declare(v.Name, value);
    }

    private object? EvaluateExpression(ExpressionNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionNode l:
                return ParseLiteral(l.Value);

            case IdentifierExpressionNode id:
                return Resolve(id.Name);

            case AssignmentExpressionNode a:
                return EvaluateAssignment(a);

            case BinaryExpressionNode b:
                return EvaluateBinary(b.Left, b.Operator, b.Right);

            case UnaryExpressionNode u:
                return EvaluateUnary(u);

            case ObjectCreationExpressionNode o:
                return CreateObject(o);

            case MemberAccessExpressionNode ma:
                return EvaluateMemberAccess(ma);

            case MethodCallExpressionNode m:
                return EvaluateMethodCall(m);

            default:
                return null;
        }
    }

    private object? EvaluateAssignment(AssignmentExpressionNode a)
    {
        if (a.Target is IdentifierExpressionNode id)
        {
            var left = Resolve(id.Name);
            var right = EvaluateExpression(a.Value);
            var result = ApplyAssignmentOperator(left, right, a.Operator);
            Assign(id.Name, result);
            return result;
        }

        if (a.Target is BinaryExpressionNode indexExpr && indexExpr.Operator == "[]")
        {
            var container = EvaluateExpression(indexExpr.Left);
            var index = Convert.ToInt32(EvaluateExpression(indexExpr.Right), CultureInfo.InvariantCulture);
            var right = EvaluateExpression(a.Value);

            if (container is object?[] arr)
            {
                var current = arr[index];
                var result = ApplyAssignmentOperator(current, right, a.Operator);
                arr[index] = result;
                return result;
            }

            if (container is IList<object?> list)
            {
                var current = list[index];
                var result = ApplyAssignmentOperator(current, right, a.Operator);
                list[index] = result;
                return result;
            }

            throw new InvalidOperationException("Index assignment is supported only for arrays and lists.");
        }

        throw new InvalidOperationException("Unsupported assignment target.");
    }

    private object? ApplyAssignmentOperator(object? left, object? right, string op)
    {
        if (op == "=")
            return right;

        if (op == "+=")
        {
            if (left is string || right is string)
                return FormatValue(left) + FormatValue(right);

            return AddNumbers(left, right);
        }

        if (op == "-=") return SubNumbers(left, right);
        if (op == "*=") return MulNumbers(left, right);
        if (op == "/=") return DivNumbers(left, right);
        if (op == "%=") return ModNumbers(left, right);

        return right;
    }

    private object? EvaluateBinary(ExpressionNode leftExpr, string op, ExpressionNode rightExpr)
    {
        if (op == "?" || op == ":" || op == "->")
        {
            var l = EvaluateExpression(leftExpr);
            var r = EvaluateExpression(rightExpr);

            return op switch
            {
                "?" => ToBool(l) ? r : null,
                ":" => r,
                "->" => r,
                _ => null
            };
        }

        if (op == "&&")
            return ToBool(EvaluateExpression(leftExpr)) && ToBool(EvaluateExpression(rightExpr));

        if (op == "||")
            return ToBool(EvaluateExpression(leftExpr)) || ToBool(EvaluateExpression(rightExpr));

        var left = EvaluateExpression(leftExpr);
        var right = EvaluateExpression(rightExpr);

        return op switch
        {
            "+" => left is string || right is string
                ? FormatValue(left) + FormatValue(right)
                : AddNumbers(left, right),

            "-" => SubNumbers(left, right),
            "*" => MulNumbers(left, right),
            "/" => DivNumbers(left, right),
            "%" => ModNumbers(left, right),

            "==" => EqualsValue(left, right),
            "!=" => !EqualsValue(left, right),

            ">" => Compare(left, right) > 0,
            "<" => Compare(left, right) < 0,
            ">=" => Compare(left, right) >= 0,
            "<=" => Compare(left, right) <= 0,

            _ => null
        };
    }

    private object? EvaluateUnary(UnaryExpressionNode u)
    {
        if (u.Operator == "throw")
        {
            var thrown = EvaluateExpression(u.Operand);
            throw new InvalidOperationException($"User throw: {FormatValue(thrown)}");
        }

        if (u.Operator == "!")
            return !ToBool(EvaluateExpression(u.Operand));

        if (u.Operator is "++" or "--")
        {
            var delta = u.Operator == "++" ? 1 : -1;

            if (u.Operand is IdentifierExpressionNode id)
            {
                var current = Resolve(id.Name);
                var updated = AddNumbers(current, delta);
                Assign(id.Name, updated);
                return u.IsPostfix ? current : updated;
            }

            if (u.Operand is BinaryExpressionNode idx && idx.Operator == "[]")
            {
                var container = EvaluateExpression(idx.Left);
                var index = Convert.ToInt32(EvaluateExpression(idx.Right), CultureInfo.InvariantCulture);

                if (container is object?[] arr)
                {
                    var current = arr[index];
                    var updated = AddNumbers(current, delta);
                    arr[index] = updated;
                    return u.IsPostfix ? current : updated;
                }

                if (container is IList<object?> list)
                {
                    var current = list[index];
                    var updated = AddNumbers(current, delta);
                    list[index] = updated;
                    return u.IsPostfix ? current : updated;
                }
            }
        }

        return EvaluateExpression(u.Operand);
    }

    private object? CreateObject(ObjectCreationExpressionNode o)
    {
        var type = o.Type;

        if (type.StartsWith("Scanner", StringComparison.Ordinal))
            return new ScannerRuntime();

        if (type.StartsWith("ArrayList", StringComparison.Ordinal))
            return new List<object?>();

        if (type.StartsWith("HashMap", StringComparison.Ordinal) || type.StartsWith("Map", StringComparison.Ordinal))
            return new Dictionary<object?, object?>();

        if (type.EndsWith("[]", StringComparison.Ordinal))
            return CreateArray(type, o.Arguments.Count > 0 ? EvaluateExpression(o.Arguments[0]) : null);

        return new JavaObject(type);
    }

    private object? CreateArray(string type, object? sizeOrInit)
    {
        if (!type.EndsWith("[]", StringComparison.Ordinal))
            return null;

        if (sizeOrInit == null)
            return Array.Empty<object?>();

        var size = Convert.ToInt32(sizeOrInit, CultureInfo.InvariantCulture);
        return new object?[size];
    }

    private object? EvaluateMemberAccess(MemberAccessExpressionNode ma)
    {
        if (ma.Target is IdentifierExpressionNode id && id.Name == "System")
        {
            if (ma.MemberName == "out")
                return ConsoleOut.Instance;

            if (ma.MemberName == "in")
                return ConsoleIn.Instance;
        }

        var target = EvaluateExpression(ma.Target);

        if (ma.MemberName == "length")
        {
            if (target is string s) return s.Length;
            if (target is Array arr) return arr.Length;
            if (target is object?[] arr2) return arr2.Length;
            if (target is ICollection col) return col.Count;
        }

        return (target, ma.MemberName) switch
        {
            (Dictionary<object?, object?> map, _) when ma.MemberName == "entrySet" => map.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value)).ToList(),
            (KeyValuePair<object?, object?> kv, "getKey") => kv.Key,
            (KeyValuePair<object?, object?> kv, "getValue") => kv.Value,
            _ => null
        };
    }

    private object? EvaluateMethodCall(MethodCallExpressionNode m)
    {
        if (m.Target is IdentifierExpressionNode id && _methods.TryGetValue(id.Name, out var method))
        {
            var args = m.Arguments.Select(EvaluateExpression).ToList();
            return InvokeMethod(method, args);
        }

        if (m.Target is MemberAccessExpressionNode ma)
        {
            var target = EvaluateExpression(ma.Target);
            var args = m.Arguments.Select(EvaluateExpression).ToList();

            if (target is ConsoleOut)
            {
                if (ma.MemberName == "print")
                {
                    Console.Write(FormatValue(args.Count > 0 ? args[0] : null));
                    return null;
                }

                if (ma.MemberName == "println")
                {
                    Console.WriteLine(FormatValue(args.Count > 0 ? args[0] : null));
                    return null;
                }
            }

            if (target is ScannerRuntime scanner)
            {
                return ma.MemberName switch
                {
                    "nextInt" => scanner.NextInt(),
                    "nextLine" => scanner.NextLine(),
                    "next" => scanner.Next(),
                    _ => null
                };
            }

            if (target is List<object?> list && ma.MemberName == "add")
            {
                list.Add(args.Count > 0 ? args[0] : null);
                return true;
            }

            if (target is Dictionary<object?, object?> map && ma.MemberName == "put")
            {
                if (args.Count >= 2)
                    map[args[0]] = args[1];
                return null;
            }

            if (target is Dictionary<object?, object?> map2 && ma.MemberName == "entrySet")
            {
                return map2.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value)).ToList();
            }

            if (target is string s && ma.MemberName == "isEmpty")
                return s.Length == 0;

            if (target is KeyValuePair<object?, object?> kv)
            {
                if (ma.MemberName == "getKey") return kv.Key;
                if (ma.MemberName == "getValue") return kv.Value;
            }
        }

        return null;
    }

    private IEnumerable<object?> Enumerate(object? value)
    {
        if (value == null)
            yield break;

        if (value is object?[] arr)
        {
            foreach (var item in arr)
                yield return item;
            yield break;
        }

        if (value is IEnumerable<object?> seq)
        {
            foreach (var item in seq)
                yield return item;
            yield break;
        }

        if (value is string s)
        {
            foreach (var ch in s)
                yield return ch.ToString();
            yield break;
        }
    }

    private object? ParseLiteral(string value)
    {
        var s = value.Trim();

        if (s == "null")
            return null;

        if (s == "true")
            return true;

        if (s == "false")
            return false;

        if (s.StartsWith("\""))
            return ParseStringLiteral(s);

        if (s.StartsWith("'"))
            return ParseCharLiteral(s);

        if (s.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            return float.Parse(NormalizeNumericLiteral(s[..^1]), CultureInfo.InvariantCulture);

        if (IsHexFloatLiteral(s))
            return ParseHexFloat(s);

        if (s.EndsWith("l", StringComparison.OrdinalIgnoreCase))
            return ParseIntegralLiteral(s[..^1], asLong: true);

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ParseHexInteger(s);

        if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return ParseBinaryInteger(s);

        if (IsLegacyOctalLiteral(s))
            return ParseOctalInteger(s);

        if (s.Contains('.') || s.Contains('e') || s.Contains('E'))
            return double.Parse(NormalizeNumericLiteral(s), CultureInfo.InvariantCulture);

        return ParseIntegralLiteral(s, asLong: false);
    }

    private bool IsHexFloatLiteral(string s)
    {
        var v = NormalizeNumericLiteral(s);
        return v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && (v.Contains('p') || v.Contains('P')) && (v.Contains('.') || v.Contains('p') || v.Contains('P'));
    }

    private string ParseStringLiteral(string s)
    {
        if (s.StartsWith("\"\"\""))
            return s;

        return s.Length >= 2 ? s[1..^1] : string.Empty;
    }

    private char ParseCharLiteral(string s)
    {
        if (s.Length >= 3 && s[1] == '\\')
        {
            return s[2] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '\'' => '\'',
                '0' => '\0',
                _ => s[2]
            };
        }

        return s.Length >= 3 ? s[1] : '\0';
    }

    private string NormalizeNumericLiteral(string s) =>
        s.Replace("_", "");

    private object ParseHexInteger(string s)
    {
        s = NormalizeNumericLiteral(s);
        if (s.EndsWith("L", StringComparison.OrdinalIgnoreCase))
            s = s[..^1];
        return Convert.ToInt64(s[2..], 16);
    }

    private object ParseBinaryInteger(string s)
    {
        s = NormalizeNumericLiteral(s);
        if (s.EndsWith("L", StringComparison.OrdinalIgnoreCase))
            s = s[..^1];
        return Convert.ToInt64(s[2..], 2);
    }

    private object ParseOctalInteger(string s)
    {
        s = NormalizeNumericLiteral(s);
        if (s.EndsWith("L", StringComparison.OrdinalIgnoreCase))
            s = s[..^1];
        return Convert.ToInt64(s[1..], 8);
    }

    private object ParseIntegralLiteral(string s, bool asLong)
    {
        s = NormalizeNumericLiteral(s);

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(s[2..], 16);

        if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(s[2..], 2);

        if (IsLegacyOctalLiteral(s))
            return Convert.ToInt64(s[1..], 8);

        return asLong ? long.Parse(s, CultureInfo.InvariantCulture) : int.Parse(s, CultureInfo.InvariantCulture);
    }

    private bool IsLegacyOctalLiteral(string s)
    {
        s = NormalizeNumericLiteral(s);
        return s.Length > 1 && s[0] == '0' && s.All(ch => ch is >= '0' and <= '7');
    }

    private double ParseHexFloat(string value)
    {
        var s = NormalizeNumericLiteral(value);

        int pIndex = s.IndexOf('p');
        if (pIndex < 0)
            pIndex = s.IndexOf('P');

        if (pIndex < 0)
            throw new FormatException($"Invalid hexadecimal floating-point literal: {value}");

        var mantissaPart = s.Substring(2, pIndex - 2);
        var exponentPart = s[(pIndex + 1)..];

        var exponent = int.Parse(exponentPart, CultureInfo.InvariantCulture);

        double mantissa;
        var dotIndex = mantissaPart.IndexOf('.');

        if (dotIndex >= 0)
        {
            var intPart = mantissaPart[..dotIndex];
            var fracPart = mantissaPart[(dotIndex + 1)..];

            mantissa = string.IsNullOrEmpty(intPart)
                ? 0.0
                : Convert.ToInt64(intPart, 16);

            for (int i = 0; i < fracPart.Length; i++)
            {
                var digit = Convert.ToInt32(fracPart[i].ToString(), 16);
                mantissa += digit / Math.Pow(16, i + 1);
            }
        }
        else
        {
            mantissa = Convert.ToInt64(mantissaPart, 16);
        }

        return mantissa * Math.Pow(2, exponent);
    }

    private bool ToBool(object? value)
    {
        if (value is bool b) return b;
        if (value is null) return false;
        if (value is string s) return s.Length > 0;
        if (value is int i) return i != 0;
        if (value is long l) return l != 0;
        if (value is double d) return Math.Abs(d) > double.Epsilon;
        if (value is float f) return Math.Abs(f) > float.Epsilon;
        return true;
    }

    private object? AddNumbers(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a) + ToDouble(b);

        return ToLong(a) + ToLong(b);
    }

    private object? SubNumbers(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a) - ToDouble(b);

        return ToLong(a) - ToLong(b);
    }

    private object? MulNumbers(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a) * ToDouble(b);

        return ToLong(a) * ToLong(b);
    }

    private object? DivNumbers(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a) / ToDouble(b);

        return ToLong(a) / ToLong(b);
    }

    private object? ModNumbers(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a) % ToDouble(b);

        return ToLong(a) % ToLong(b);
    }

    private bool IsFloating(object? v) => v is float or double or decimal;

    private long ToLong(object? value)
    {
        if (value is null) return 0;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is char c) return c;
        if (value is float f) return (long)f;
        if (value is double d) return (long)d;
        if (value is string str && long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return n;
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private double ToDouble(object? value)
    {
        if (value is null) return 0.0;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is char c) return c;
        if (value is float f) return f;
        if (value is double d) return d;
        if (value is string str && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return n;
        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private int Compare(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return ToDouble(a).CompareTo(ToDouble(b));

        return ToLong(a).CompareTo(ToLong(b));
    }

    private bool EqualsValue(object? a, object? b)
    {
        if (IsFloating(a) || IsFloating(b))
            return Math.Abs(ToDouble(a) - ToDouble(b)) < double.Epsilon;

        return Equals(NormalizeValue(a), NormalizeValue(b));
    }

    private object? NormalizeValue(object? value)
    {
        if (value is int or long or short or byte or char)
            return ToLong(value);

        if (value is float or double or decimal)
            return ToDouble(value);

        return value;
    }

    private string FormatValue(object? value)
    {
        if (value is null) return "null";
        if (value is bool b) return b ? "true" : "false";
        if (value is string s) return s;
        if (value is char c) return c.ToString();
        if (value is IEnumerable<object?> seq && value is not string)
            return "[" + string.Join(", ", seq.Select(FormatValue)) + "]";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private object? Resolve(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value))
                return value;
        }

        return null;
    }

    private void Declare(string name, object? value)
    {
        _scopes.Peek()[name] = value;
    }

    private void Assign(string name, object? value)
    {
        foreach (var scope in _scopes)
        {
            if (scope.ContainsKey(name))
            {
                scope[name] = value;
                return;
            }
        }

        _scopes.Peek()[name] = value;
    }

    private bool IsDeclaredInCurrentScope(string name) =>
        _scopes.Peek().ContainsKey(name);

    private void EnterScope() =>
        _scopes.Push(new Dictionary<string, object?>());

    private void ExitScope() =>
        _scopes.Pop();

    private sealed class ReturnSignal : Exception
    {
        public ReturnSignal(object? value) => Value = value;
        public object? Value { get; }
    }

    private sealed class BreakSignal : Exception { }

    private sealed class ContinueSignal : Exception { }

    private sealed class JavaObject
    {
        public JavaObject(string type) => Type = type;
        public string Type { get; }
        public override string ToString() => $"new {Type}()";
    }

    private sealed class ScannerRuntime
    {
        public int NextInt()
        {
            var line = Console.ReadLine() ?? "0";
            return int.TryParse(line.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        public string NextLine() => Console.ReadLine() ?? string.Empty;

        public string Next()
        {
            var line = Console.ReadLine() ?? string.Empty;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }
    }

    private sealed class ConsoleOut
    {
        public static readonly ConsoleOut Instance = new();
        private ConsoleOut() { }
    }

    private sealed class ConsoleIn
    {
        public static readonly ConsoleIn Instance = new();
        private ConsoleIn() { }
    }
}