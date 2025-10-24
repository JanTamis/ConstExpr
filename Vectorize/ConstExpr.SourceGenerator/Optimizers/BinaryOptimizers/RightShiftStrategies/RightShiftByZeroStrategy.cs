using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
/// Strategy for shift by zero: x >> 0 => x
/// </summary>
public class RightShiftByZeroStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericZero();
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
