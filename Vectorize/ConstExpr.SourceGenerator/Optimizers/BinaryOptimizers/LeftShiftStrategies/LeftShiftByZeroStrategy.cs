using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for shift by zero: x << 0 => x (pure)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class LeftShiftByZeroStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsNumericZero())
    {
      return false;
    }

    optimized = context.Left.Syntax;
		return true;
	}
}
