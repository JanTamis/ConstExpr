using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: (power of two) * x => x << n (integer)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class MultiplyByPowerOfTwoLeftStrategy : IntegerBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsNumericPowerOfTwo(out var power))
    {
      return false;
    }

    optimized = LeftShiftExpression(
			context.Right.Syntax,
			CreateLiteral(power));
		return true;
	}
}
