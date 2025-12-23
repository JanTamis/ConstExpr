using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class ConditionalPatternCombinersStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is IsPatternExpressionSyntax left
		       && context.Right.Syntax is IsPatternExpressionSyntax right
		       && left.Expression.IsEquivalentTo(right.Expression);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not IsPatternExpressionSyntax left
		    || context.Right.Syntax is not IsPatternExpressionSyntax right)
		{
			return null;
		}
		
		var patternKind = GetRelationalPatternKind(context.Kind);
		
		if (patternKind == SyntaxKind.None)
		{
			return null;
		}
		
		var combinedPattern = SyntaxFactory.BinaryPattern(
			patternKind,
			left.Pattern,
			right.Pattern);
		
		return SyntaxFactory.IsPatternExpression(left.Expression, combinedPattern);
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