using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for De Morgan's law: !a && !b → !(a || b)
/// This can reduce the number of negations and may help branch prediction.
/// </summary>
public class ConditionalAndDeMorganStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		// Check if both sides are negations: !a && !b
		return context.Left.Syntax is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } &&
		       context.Right.Syntax is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken };
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var leftNegation = (PrefixUnaryExpressionSyntax)context.Left.Syntax;
		var rightNegation = (PrefixUnaryExpressionSyntax)context.Right.Syntax;

		// !a && !b → !(a || b)
		var orExpression = BinaryExpression(
			SyntaxKind.LogicalOrExpression,
			leftNegation.Operand,
			rightNegation.Operand);

		return PrefixUnaryExpression(
			SyntaxKind.LogicalNotExpression,
			ParenthesizedExpression(orExpression));
	}
}

