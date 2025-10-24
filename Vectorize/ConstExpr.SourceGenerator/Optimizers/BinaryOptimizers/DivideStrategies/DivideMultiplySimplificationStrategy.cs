using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for algebraic simplification: (x * a) / a => x
/// </summary>
public class DivideMultiplySimplificationStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMul
			&& ((leftMul.Right.IsEquivalentTo(context.Right.Syntax) && IsPure(leftMul.Left))
				|| (leftMul.Left.IsEquivalentTo(context.Right.Syntax) && IsPure(leftMul.Right)));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMul)
		{
			return null;
		}

		// Check if right side of multiply matches divisor
		if (leftMul.Right.IsEquivalentTo(context.Right.Syntax)
				&& IsPure(leftMul.Left))
		{
			return leftMul.Left;
		}

		// Check if left side of multiply matches divisor
		if (leftMul.Left.IsEquivalentTo(context.Right.Syntax)
				&& IsPure(leftMul.Right))
		{
			return leftMul.Right;
		}

		return null;
	}
}
