using System.Text;
using JavaTranslator.Ast;

namespace JavaTranslator.Utils;

public class AstVisualizer
{
    private readonly StringBuilder _sb = new();

    public string Visualize(AstNode node)
    {
        _sb.Clear();
        Print(node, "", true);
        return _sb.ToString();
    }

    private void Print(AstNode node, string indent, bool isLast)
    {
        _sb.Append(indent);
        _sb.Append(isLast ? "└── " : "├── ");
        _sb.Append(node.GetType().Name.Replace("Node", ""));

        var extra = node switch
        {
            ImportNode n => $": {n.Path}",
            ClassDeclNode n => "$: {n.Name}",
            MethodDeclNode n => $": {n.ReturnType} {n.Name}",
            VariableDeclNode n => $": {n.Type} {n.Name}",
            IdentifierExpressionNode n => $": {n.Name}",
            LiteralExpressionNode n => $": {n.Value}",
            BinaryExpressionNode n => $": {n.Operator}",
            UnaryExpressionNode n => $": {n.Operator} (Postfix: {n.IsPostfix})",
            AssignmentExpressionNode n => $": {n.Operator}",
            MemberAccessExpressionNode n => $": .{n.MemberName}",
            ObjectCreationExpressionNode n => $": new {n.Type}",
            _ => ""
        };

        _sb.AppendLine(extra);

        indent += isLast ? "    " : "│   ";

        var children = GetChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            Print(children[i], indent, i == children.Count - 1);
        }
    }

    private List<AstNode> GetChildren(AstNode node)
    {
        var list = new List<AstNode>();
        switch (node)
        {
            case CompilationUnitNode n:
                list.AddRange(n.Imports);
                list.AddRange(n.Classes);
                break;
            case ClassDeclNode n:
                list.AddRange(n.Members);
                break;
            case MethodDeclNode n:
                list.AddRange(n.Parameters);
                list.Add(n.Body);
                break;
            case BlockStatementNode n:
                list.AddRange(n.Statements);
                break;
            case VariableDeclNode n:
                if (n.Initializer != null) list.Add(n.Initializer);
                break;
            case ExpressionStatementNode n:
                list.Add(n.Expression);
                break;
            case IfStatementNode n:
                list.Add(n.Condition);
                list.Add(n.ThenBranch);
                if (n.ElseBranch != null) list.Add(n.ElseBranch);
                break;
            case WhileStatementNode n:
                list.Add(n.Condition);
                list.Add(n.Body);
                break;
            case DoWhileStatementNode n:
                list.Add(n.Body);
                list.Add(n.Condition);
                break;
            case ForStatementNode n:
                if (n.Initialization != null) list.Add(n.Initialization);
                if (n.Condition != null) list.Add(n.Condition);
                if (n.Increment != null) list.Add(n.Increment);
                list.Add(n.Body);
                break;
            case ReturnStatementNode n:
                if (n.Value != null) list.Add(n.Value);
                break;
            case BinaryExpressionNode n:
                list.Add(n.Left);
                list.Add(n.Right);
                break;
            case UnaryExpressionNode n:
                list.Add(n.Operand);
                break;
            case AssignmentExpressionNode n:
                list.Add(n.Target);
                list.Add(n.Value);
                break;
            case MemberAccessExpressionNode n:
                list.Add(n.Target);
                break;
            case MethodCallExpressionNode n:
                list.Add(n.Target);
                list.AddRange(n.Arguments);
                break;
            case ObjectCreationExpressionNode n:
                list.AddRange(n.Arguments);
                break;
        }
        return list;
    }
}