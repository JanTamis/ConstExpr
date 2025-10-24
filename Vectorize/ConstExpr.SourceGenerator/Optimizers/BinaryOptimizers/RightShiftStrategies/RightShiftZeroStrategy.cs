using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
/// Strategy for shifting zero: 0 >> x => 0 (pure)
/// </summary>
public class RightShiftZeroStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericZero()
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
	}
}
