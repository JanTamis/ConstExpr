using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for subtraction cancellation optimization:
/// (x - a) + a => x (algebraic identity, pure)
/// a + (x - a) => x (algebraic identity, pure)
/// </summary>
public class AddSubtractionCancellationStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return CanOptimizeLeftSubtraction(context) || CanOptimizeRightSubtraction(context);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// (x - a) + a => x (algebraic identity, pure)
		if (CanOptimizeLeftSubtraction(context))
		{
			var leftSub = (BinaryExpressionSyntax)context.Left.Syntax;
			return leftSub.Left;
		}

		// a + (x - a) => x (algebraic identity, pure)
		if (CanOptimizeRightSubtraction(context))
		{
			var rightSub = (BinaryExpressionSyntax)context.Right.Syntax;
			return rightSub.Left;
		}

		return null;
	}

	private static bool CanOptimizeLeftSubtraction(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax leftSub
			&& leftSub.RawKind == (int)SyntaxKind.SubtractExpression
			&& leftSub.Right.IsEquivalentTo(context.Right.Syntax)
			&& IsPure(leftSub.Left) && IsPure(leftSub.Right) && IsPure(context.Right.Syntax);
	}

	private static bool CanOptimizeRightSubtraction(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is BinaryExpressionSyntax rightSub
				&& rightSub.RawKind == (int)SyntaxKind.SubtractExpression
				&& rightSub.Right.IsEquivalentTo(context.Left.Syntax)
				&& IsPure(context.Left.Syntax) && IsPure(rightSub.Left) && IsPure(rightSub.Right);
	}
}
