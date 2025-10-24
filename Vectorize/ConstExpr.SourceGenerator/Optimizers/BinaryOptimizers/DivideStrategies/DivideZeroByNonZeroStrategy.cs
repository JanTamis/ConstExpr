using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for zero divided by non-zero: 0 / x = 0 (integers only)
/// </summary>
public class DivideZeroByNonZeroStrategy : SymmetricStrategy<IntegerBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left is { HasValue: true, Value: { } leftValue }
					 && leftValue.IsNumericZero();
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
	}
}
