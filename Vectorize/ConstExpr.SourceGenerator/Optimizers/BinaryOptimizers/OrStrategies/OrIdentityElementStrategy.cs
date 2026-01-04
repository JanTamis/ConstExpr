using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for identity element: x | 0 = x and 0 | x = x
/// </summary>
public class OrIdentityElementStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.HasValue && context.Right.Value.IsNumericZero();
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
