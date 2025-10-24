using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for zero modulo non-zero: 0 % c = 0 (when c != 0)
/// </summary>
public class ModuloZeroStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericZero()
		       && context.Right.HasValue 
		       && !context.Right.Value.IsNumericZero();
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
	}
}
