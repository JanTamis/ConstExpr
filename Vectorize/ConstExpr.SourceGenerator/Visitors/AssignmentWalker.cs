using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Visitors;

public class AssignmentWalker(SemanticModel semanticModel) : CSharpSyntaxWalker
{
	public HashSet<string> AssignedVariables { get; } = [ ];

	public static IEnumerable<string> GetAssignedVariables(SyntaxNode node, SemanticModel semanticModel)
	{
		var visitor = new AssignmentWalker(semanticModel);
		visitor.Visit(node);
		return visitor.AssignedVariables;
	}

	// x = ..., x += ..., x -= ..., etc.
	public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (node.Left is IdentifierNameSyntax id)
		{
			AssignedVariables.Add(id.Identifier.Text);
		}

		base.VisitAssignmentExpression(node);
	}

	// x++, x--
	public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		if (node.IsKind(SyntaxKind.PostIncrementExpression) ||
		    node.IsKind(SyntaxKind.PostDecrementExpression))
		{
			if (node.Operand is IdentifierNameSyntax id)
			{
				AssignedVariables.Add(id.Identifier.Text);
			}
		}

		base.VisitPostfixUnaryExpression(node);
	}

	// ++x, --x
	public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		if (node.IsKind(SyntaxKind.PreIncrementExpression) ||
		    node.IsKind(SyntaxKind.PreDecrementExpression))
		{
			if (node.Operand is IdentifierNameSyntax id)
			{
				AssignedVariables.Add(id.Identifier.Text);
			}
		}

		base.VisitPrefixUnaryExpression(node);
	}
}