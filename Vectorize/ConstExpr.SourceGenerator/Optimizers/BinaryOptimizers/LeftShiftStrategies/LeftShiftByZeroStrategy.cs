using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for shift by zero: x << 0 => x (pure)
/// </summary>
public class LeftShiftByZeroStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericZero()
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
