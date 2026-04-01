using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: x * (power of two) => x << n (integer)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class MultiplyByPowerOfTwoRightStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsNumericPowerOfTwo(out var power))
    {
      return false;
    }

    optimized = LeftShiftExpression(context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		return true;
	}
}
