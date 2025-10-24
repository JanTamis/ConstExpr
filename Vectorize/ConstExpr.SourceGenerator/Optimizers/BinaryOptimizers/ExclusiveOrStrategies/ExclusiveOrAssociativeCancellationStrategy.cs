using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for associative cancellation: (x ^ y) ^ x = y (pure)
/// </summary>
public class ExclusiveOrAssociativeCancellationStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ExclusiveOrExpression } xorLeft
		       && IsPure(context.Right.Syntax) 
		       && IsPure(xorLeft.Left) 
		       && IsPure(xorLeft.Right)
		       && (context.Right.Syntax.IsEquivalentTo(xorLeft.Left) || context.Right.Syntax.IsEquivalentTo(xorLeft.Right));
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		var xorLeft = (BinaryExpressionSyntax)context.Left.Syntax;

		if (context.Right.Syntax.IsEquivalentTo(xorLeft.Left))
			return xorLeft.Right;

		if (context.Right.Syntax.IsEquivalentTo(xorLeft.Right))
			return xorLeft.Left;

		return null;
	}
}
