using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// (x | mask) & mask => mask (when x is pure)
/// symmetric
/// </summary>
public class AndOrMaskCollisionStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseOrExpression }
		       && context.Right.HasValue
		       && context.Left.Value != null
		       && Equals(context.Left.Value, context.Right.Value)
		       && SyntaxHelpers.TryGetLiteral(context.Right.Value, out _);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (SyntaxHelpers.TryGetLiteral(context.Right.Value, out var lit))
		{
			// if masks equal -> return mask literal
			return lit;
		}

		return null;
	}
}
