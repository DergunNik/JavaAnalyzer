using System.Collections.Generic;
using System.Text;
using JavaTranslator.Ast;

namespace JavaTranslator.Utils;

public class AstVisualizer2
{
    private readonly StringBuilder _sb = new();

    public string Visualize(AstNode node)
    {
        _sb.Clear();
        PrintNode(node, "", true);
        return _sb.ToString();
    }

    private void PrintNode(AstNode node, string indent, bool isLast)
    {
        _sb.Append(indent);
        _sb.Append(isLast ? "└── " : "├── ");

        var label = GetNodeLabel(node);
        _sb.AppendLine(label);

        var children = GetChildren(node);
        var newIndent = indent + (isLast ? "    " : "│   ");

        for (int i = 0; i < children.Count; i++)
        {
            PrintNode(children[i], newIndent, i == children.Count - 1);
        }
    }

    private string GetNodeLabel(AstNode node)
    {
        return node switch
        {
            CompilationUnitNode => "CompilationUnit (Root)",
            ImportNode n => $"Import: {n.Path}",
            ClassDeclNode n => $"Class: {n.Name}",
            MethodDeclNode n => $"Method: {n.ReturnType} {n.Name}",
            ParameterNode n => $"Parameter: {n.Type} {n.Name}",
            BlockStatementNode => "Block { }",
            VariableDeclNode n => $"VarDecl: {n.Type} {n.Name}",
            ExpressionStatementNode => "ExpressionStmt",
            IfStatementNode => "IfStatement",
            WhileStatementNode => "WhileStatement",
            DoWhileStatementNode => "DoWhileStatement",
            ForStatementNode => "ForStatement",
            ReturnStatementNode => "ReturnStatement",
            BreakStatementNode => "Break",
            ContinueStatementNode => "Continue",
            BinaryExpressionNode n => $"BinaryExpr (Op: {n.Operator})",
            UnaryExpressionNode n => $"UnaryExpr (Op: {n.Operator}, Postfix: {n.IsPostfix})",
            AssignmentExpressionNode n => $"Assignment (Op: {n.Operator})",
            LiteralExpressionNode n => $"Literal: {n.Value}",
            IdentifierExpressionNode n => $"Identifier: {n.Name}",
            MemberAccessExpressionNode n => $"MemberAccess: .{n.MemberName}",
            MethodCallExpressionNode => "MethodCall",
            ObjectCreationExpressionNode n => $"NewObject: {n.Type}",
            _ => node.GetType().Name.Replace("Node", "")
        };
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