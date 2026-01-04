using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for identity element: x * 1 = x and 1 * x = x
/// </summary>
public class MultiplyIdentityElementStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.HasValue && context.Right.Value.IsNumericOne();
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
