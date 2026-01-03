using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for shift by zero: x << 0 => x (pure)
/// </summary>
public class LeftShiftByZeroStrategy : IntegerBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || !rightValue.IsNumericZero())
			return false;
		
		optimized = context.Left.Syntax;
		return true;
	}
}
