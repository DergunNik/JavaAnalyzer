using System.Collections.Generic;

namespace JavaTranslator.Ast;

public abstract class AstNode { }

public class CompilationUnitNode : AstNode
{
    public List<ImportNode> Imports { get; } = new();
    public List<ClassDeclNode> Classes { get; } = new();
}

public class ImportNode : AstNode
{
    public string Path { get; set; } = string.Empty;
}

public class ClassDeclNode : AstNode
{
    public string Name { get; set; } = string.Empty;
    public List<AstNode> Members { get; } = new();
}

public class ParameterNode : AstNode
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MethodDeclNode : AstNode
{
    public string ReturnType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ParameterNode> Parameters { get; } = new();
    public BlockStatementNode Body { get; set; } = new();
}

public abstract class StatementNode : AstNode { }

public class BlockStatementNode : StatementNode
{
    public List<StatementNode> Statements { get; } = new();
}

public class VariableDeclNode : StatementNode
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExpressionNode? Initializer { get; set; }
}

public class ExpressionStatementNode : StatementNode
{
    public ExpressionNode Expression { get; set; } = new ExpressionNode();
}

public class IfStatementNode : StatementNode
{
    public ExpressionNode Condition { get; set; } = new ExpressionNode();
    public StatementNode ThenBranch { get; set; } = new BlockStatementNode();
    public StatementNode? ElseBranch { get; set; }
}

public class WhileStatementNode : StatementNode
{
    public ExpressionNode Condition { get; set; } = new ExpressionNode();
    public StatementNode Body { get; set; } = new BlockStatementNode();
}

public class DoWhileStatementNode : StatementNode
{
    public StatementNode Body { get; set; } = new BlockStatementNode();
    public ExpressionNode Condition { get; set; } = new ExpressionNode();
}

public class ForStatementNode : StatementNode
{
    public AstNode? Initialization { get; set; }
    public ExpressionNode? Condition { get; set; }
    public ExpressionNode? Increment { get; set; }
    public StatementNode Body { get; set; } = new BlockStatementNode();
}

public class ReturnStatementNode : StatementNode
{
    public ExpressionNode? Value { get; set; }
}

public class BreakStatementNode : StatementNode { }
public class ContinueStatementNode : StatementNode { }

public class ExpressionNode : AstNode { }

public class BinaryExpressionNode : ExpressionNode
{
    public ExpressionNode Left { get; set; } = new ExpressionNode();
    public string Operator { get; set; } = string.Empty;
    public ExpressionNode Right { get; set; } = new ExpressionNode();
}

public class UnaryExpressionNode : ExpressionNode
{
    public string Operator { get; set; } = string.Empty;
    public ExpressionNode Operand { get; set; } = new ExpressionNode();
    public bool IsPostfix { get; set; }
}

public class AssignmentExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; } = new ExpressionNode();
    public string Operator { get; set; } = string.Empty;
    public ExpressionNode Value { get; set; } = new ExpressionNode();
}

public class LiteralExpressionNode : ExpressionNode
{
    public string Value { get; set; } = string.Empty;
}

public class IdentifierExpressionNode : ExpressionNode
{
    public string Name { get; set; } = string.Empty;
}

public class MemberAccessExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; } = new ExpressionNode();
    public string MemberName { get; set; } = string.Empty;
}

public class MethodCallExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; } = new ExpressionNode();
    public List<ExpressionNode> Arguments { get; } = new();
}

public class ObjectCreationExpressionNode : ExpressionNode
{
    public string Type { get; set; } = string.Empty;
    public List<ExpressionNode> Arguments { get; } = new();
}