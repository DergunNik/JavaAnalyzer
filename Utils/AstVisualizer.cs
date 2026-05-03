using System;
using System.Collections.Generic;
using System.Text;
using JavaTranslator.Ast;

namespace JavaTranslator.Utils;

public class AstVisualizer
{
    private readonly StringBuilder _sb = new();

    public string Visualize(AstNode node)
    {
        _sb.Clear();

        if (node == null)
            return string.Empty;

        var displayRoot = BuildDisplayNode(node);
        var rendered = RenderDisplayNode(displayRoot);

        for (int i = 0; i < rendered.Lines.Count; i++)
            _sb.AppendLine(rendered.Lines[i].TrimEnd());

        return _sb.ToString();
    }

    private sealed class DisplayNode
    {
        public string Label { get; }
        public List<DisplayNode> Children { get; }

        public DisplayNode(string label, List<DisplayNode> children)
        {
            Label = label;
            Children = children;
        }
    }

    private sealed class RenderResult
    {
        public List<string> Lines { get; }
        public int Width { get; }
        public int Center { get; }

        public RenderResult(List<string> lines, int width, int center)
        {
            Lines = lines;
            Width = width;
            Center = center;
        }
    }

    private static DisplayNode Leaf(string label) => new(label, new List<DisplayNode>());

    private DisplayNode BuildDisplayNode(AstNode node)
    {
        if (node is VariableDeclNode varDecl)
        {
            var init = varDecl.Initializer == null ? null : BuildDisplayNode(varDecl.Initializer);
            var left = Leaf($"{varDecl.Type} {varDecl.Name}");

            if (init == null)
                return new DisplayNode("VarDecl", new List<DisplayNode> { left });

            return new DisplayNode("VarDecl", new List<DisplayNode>
            {
                left,
                Leaf("="),
                init
            });
        }

        if (node is ExpressionStatementNode exprStmt)
        {
            if (exprStmt.Expression is AssignmentExpressionNode assignment)
            {
                return new DisplayNode("ExpressionStmt", new List<DisplayNode>
                {
                    BuildDisplayNode(assignment.Target),
                    Leaf(assignment.Operator),
                    BuildDisplayNode(assignment.Value)
                });
            }

            return new DisplayNode("ExpressionStmt", new List<DisplayNode>
            {
                BuildDisplayNode(exprStmt.Expression)
            });
        }

        if (node is BinaryExpressionNode binary)
        {
            return new DisplayNode("BinaryExpr", new List<DisplayNode>
            {
                BuildDisplayNode(binary.Left),
                Leaf(binary.Operator),
                BuildDisplayNode(binary.Right)
            });
        }

        if (node is AssignmentExpressionNode assignmentNode)
        {
            return new DisplayNode("Assignment", new List<DisplayNode>
            {
                BuildDisplayNode(assignmentNode.Target),
                Leaf(assignmentNode.Operator),
                BuildDisplayNode(assignmentNode.Value)
            });
        }

        if (node is UnaryExpressionNode unary)
        {
            if (unary.IsPostfix)
            {
                return new DisplayNode("UnaryExpr", new List<DisplayNode>
                {
                    BuildDisplayNode(unary.Operand),
                    Leaf(unary.Operator)
                });
            }

            return new DisplayNode("UnaryExpr", new List<DisplayNode>
            {
                Leaf(unary.Operator),
                BuildDisplayNode(unary.Operand)
            });
        }

        if (node is MethodCallExpressionNode call)
        {
            var children = new List<DisplayNode> { BuildDisplayNode(call.Target) };
            foreach (var arg in call.Arguments)
                children.Add(BuildDisplayNode(arg));
            return new DisplayNode("Call", children);
        }

        if (node is MemberAccessExpressionNode member)
        {
            return new DisplayNode("MemberAccess", new List<DisplayNode>
            {
                BuildDisplayNode(member.Target),
                Leaf($".{member.MemberName}")
            });
        }

        if (node is ObjectCreationExpressionNode obj)
        {
            var children = new List<DisplayNode>();
            foreach (var arg in obj.Arguments)
                children.Add(BuildDisplayNode(arg));
            return new DisplayNode($"new {EscapeLabel(obj.Type)}", children);
        }

        if (node is LiteralExpressionNode lit)
            return Leaf(EscapeLabel(lit.Value));

        if (node is IdentifierExpressionNode id)
            return Leaf(EscapeLabel(id.Name));

        var childrenNodes = GetChildren(node);
        var label = GetNodeLabel(node);
        var childNodes = new List<DisplayNode>(childrenNodes.Count);

        foreach (var child in childrenNodes)
            childNodes.Add(BuildDisplayNode(child));

        return new DisplayNode(label, childNodes);
    }

    private RenderResult RenderDisplayNode(DisplayNode node)
    {
        if (node.Children.Count == 0)
        {
            return new RenderResult(
                new List<string> { node.Label },
                node.Label.Length,
                node.Label.Length / 2
            );
        }

        const int gap = 3;

        var childResults = new List<RenderResult>(node.Children.Count);
        foreach (var child in node.Children)
            childResults.Add(RenderDisplayNode(child));

        int childrenWidth = 0;
        for (int i = 0; i < childResults.Count; i++)
        {
            childrenWidth += childResults[i].Width;
            if (i > 0)
                childrenWidth += gap;
        }

        var childCentersRaw = new List<int>(childResults.Count);
        int offset = 0;
        for (int i = 0; i < childResults.Count; i++)
        {
            childCentersRaw.Add(offset + childResults[i].Center);
            offset += childResults[i].Width + gap;
        }

        int midIndex = (childCentersRaw.Count - 1) / 2;
        int rawParentCenter = childCentersRaw[midIndex];

        int leftHalf = node.Label.Length / 2;
        int rightHalf = node.Label.Length - leftHalf;

        int shift = Math.Max(0, -(rawParentCenter - leftHalf));
        int totalWidth = Math.Max(childrenWidth + shift, rawParentCenter + rightHalf + shift);

        var childCenters = new List<int>(childCentersRaw.Count);
        for (int i = 0; i < childCentersRaw.Count; i++)
            childCenters.Add(childCentersRaw[i] + shift);

        int parentCenter = rawParentCenter + shift;
        int labelStart = parentCenter - leftHalf;

        var lines = new List<string>();

        var firstLine = new char[totalWidth];
        Array.Fill(firstLine, ' ');
        for (int i = 0; i < node.Label.Length; i++)
        {
            int pos = labelStart + i;
            if (pos >= 0 && pos < totalWidth)
                firstLine[pos] = node.Label[i];
        }
        lines.Add(new string(firstLine));

        lines.Add(BuildConnectorLine(parentCenter, childCenters, totalWidth));

        int maxHeight = 0;
        for (int i = 0; i < childResults.Count; i++)
            maxHeight = Math.Max(maxHeight, childResults[i].Lines.Count);

        int childX0 = shift;

        for (int row = 0; row < maxHeight; row++)
        {
            var rowChars = new char[totalWidth];
            Array.Fill(rowChars, ' ');

            int xPos = childX0;
            for (int i = 0; i < childResults.Count; i++)
            {
                var child = childResults[i];
                var text = row < child.Lines.Count ? child.Lines[row] : string.Empty;

                for (int j = 0; j < text.Length; j++)
                {
                    int pos = xPos + j;
                    if (pos >= 0 && pos < totalWidth)
                        rowChars[pos] = text[j];
                }

                xPos += child.Width + gap;
            }

            lines.Add(new string(rowChars));
        }

        return new RenderResult(lines, totalWidth, parentCenter);
    }

    private static string BuildConnectorLine(int parentCenter, List<int> childCenters, int width)
    {
        if (width <= 0)
            return string.Empty;

        var chars = new char[width];
        Array.Fill(chars, ' ');

        if (childCenters.Count == 0)
            return new string(chars);

        if (childCenters.Count == 1)
        {
            int c = childCenters[0];
            if (c >= 0 && c < width)
                chars[c] = '│';
            return new string(chars);
        }

        int left = childCenters[0];
        int right = childCenters[childCenters.Count - 1];

        int min = Math.Min(parentCenter, left);
        int max = Math.Max(parentCenter, right);

        for (int i = min; i <= max && i < width; i++)
        {
            if (i >= 0)
                chars[i] = '─';
        }

        for (int i = 0; i < childCenters.Count; i++)
        {
            int c = childCenters[i];
            if (c < 0 || c >= width)
                continue;

            if (c == parentCenter)
            {
                chars[c] = '│';
                continue;
            }

            if (i == 0)
                chars[c] = '┌';
            else if (i == childCenters.Count - 1)
                chars[c] = '┐';
            else
                chars[c] = '┬';
        }

        if (parentCenter >= 0 && parentCenter < width && chars[parentCenter] == ' ')
            chars[parentCenter] = '┴';

        return new string(chars);
    }

    private string GetNodeLabel(AstNode node)
    {
        return node switch
        {
            CompilationUnitNode => "CompilationUnit (Root)",
            ImportNode n => $"Import: {EscapeLabel(n.Path)}",
            ClassDeclNode n => $"Class: {EscapeLabel(n.Name)}",
            MethodDeclNode n => $"Method: {EscapeLabel(n.ReturnType)} {EscapeLabel(n.Name)}",
            ParameterNode n => $"Parameter: {EscapeLabel(n.Type)} {EscapeLabel(n.Name)}",
            BlockStatementNode => "Block { }",
            ExpressionStatementNode => "ExpressionStmt",
            IfStatementNode => "IfStatement",
            WhileStatementNode => "WhileStatement",
            DoWhileStatementNode => "DoWhileStatement",
            ForStatementNode => "ForStatement",
            ReturnStatementNode => "ReturnStatement",
            BreakStatementNode => "Break",
            ContinueStatementNode => "Continue",
            LiteralExpressionNode n => EscapeLabel(n.Value),
            IdentifierExpressionNode n => EscapeLabel(n.Name),
            _ => node.GetType().Name.Replace("Node", string.Empty)
        };
    }

    private static string EscapeLabel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static List<AstNode> GetChildren(AstNode node)
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
        }

        return list;
    }
}