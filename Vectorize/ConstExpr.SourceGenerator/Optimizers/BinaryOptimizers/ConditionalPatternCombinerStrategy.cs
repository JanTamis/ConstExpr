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

		var patternKind = GetRelationalPatternKind(context.Kind);
		if (patternKind == SyntaxKind.None)
		{
			return null;
		}

		if (left.Expression.IsEquivalentTo(right.Left))
		{
			var rightPattern = ConvertToPattern(right.OperatorToken.Kind(), right.Right);
			if (rightPattern == null)
			{
				return null;
			}

			var combinedPattern = SyntaxFactory.BinaryPattern(
				patternKind,
				left.Pattern,
				rightPattern);

			return SyntaxFactory.IsPatternExpression(left.Expression, combinedPattern);
		}

		if (left.Expression.IsEquivalentTo(right.Right))
		{
			var rightPattern = ConvertToPattern(SwapCondition(right.OperatorToken.Kind()), right.Left);
			if (rightPattern == null)
			{
				return null;
			}

			var combinedPattern = SyntaxFactory.BinaryPattern(
				patternKind,
				left.Pattern,
				rightPattern);

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
			_ => SyntaxKind.None
		};
	}
}