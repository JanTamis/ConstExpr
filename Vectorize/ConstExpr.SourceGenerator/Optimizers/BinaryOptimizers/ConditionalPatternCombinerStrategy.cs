using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class ConditionalPatternCombinerStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is IsPatternExpressionSyntax left
		       && context.Right.Syntax is BinaryExpressionSyntax right
		       && (left.Expression.IsEquivalentTo(right.Left) || left.Expression.IsEquivalentTo(right.Right));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not IsPatternExpressionSyntax left
		    || context.Right.Syntax is not BinaryExpressionSyntax right)
		{
			return null;
		}

		if (left.Expression.IsEquivalentTo(right.Left))
		{
			var combinedPattern = SyntaxFactory.BinaryPattern(
				GetRelationalPatternKind(context.Kind),
				left.Pattern,
				SyntaxFactory.RelationalPattern(right.OperatorToken, right.Right));

			return SyntaxFactory.IsPatternExpression(left.Expression, combinedPattern);
		}

		if (left.Expression.IsEquivalentTo(right.Right))
		{
			var combinedPattern = SyntaxFactory.BinaryPattern(
				GetRelationalPatternKind(context.Kind),
				left.Pattern,
				SyntaxFactory.RelationalPattern(SyntaxFactory.Token(SwapCondition(right.OperatorToken.Kind())), right.Left));

			return SyntaxFactory.IsPatternExpression(left.Expression, combinedPattern);
		}

		return null;
	}

	private SyntaxKind GetRelationalPatternKind(BinaryOperatorKind operatorKind)
	{
		return operatorKind switch
		{
			BinaryOperatorKind.ConditionalAnd => SyntaxKind.AndPattern,
			BinaryOperatorKind.ConditionalOr => SyntaxKind.OrPattern,
		};
	}
}