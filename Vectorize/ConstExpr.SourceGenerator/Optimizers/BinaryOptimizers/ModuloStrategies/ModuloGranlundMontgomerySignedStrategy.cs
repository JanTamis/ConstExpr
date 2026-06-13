using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
///   Granlund-Montgomery reduction for signed 32-bit modulo by a positive constant
///   non-power-of-2 divisor. The quotient is computed with a signed multiply-high +
///   arithmetic shift and a sign-bit correction (round toward zero), and the remainder
///   is recovered as <c>x - d * (x / d)</c>.
///   q  = (int)((long)x * MAGIC >> 32);
///   q += x;                      // only when MAGIC wrapped negative
///   q >>= shift;                 // arithmetic, only when shift > 0
///   q -= x >> 31;                // +1 for negative dividends (truncation toward zero)
///   x % d  =>  x - q * d
///   Examples:
///   x % 6  =>  x - ((int)((long)x * 715827883 >> 32) - (x >> 31)) * 6
/// </summary>
public class ModuloGranlundMontgomerySignedStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.MagicNumberDivision ];

	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType == SpecialType.System_Int32;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		if (context.Right.Syntax.Token.Value is not int d
		    || d <= 1
		    || (d & d - 1) == 0 // power of 2 — already handled by ModuloByPowerOfTwoStrategy
		    || !IsPure(context.Left.Syntax)) // x appears multiple times in the generated expression
		{
			return false;
		}

		var x = context.Left.Syntax;
		var quotient = GranlundMontgomeryEmitter.BuildSignedQuotient(x, d);

		// x - q * d
		optimized = SubtractExpression(x, MultiplyExpression(quotient, CreateLiteral(d)));
		return true;
	}
}