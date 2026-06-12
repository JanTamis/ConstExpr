using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Granlund-Montgomery division for signed 32-bit division by a positive constant
///   non-power-of-2 divisor: replaces the division instruction with a signed multiply-high +
///   arithmetic shift and a sign-bit correction (round toward zero).
///   q  = (int)((long)x * MAGIC >> 32);
///   q += x;                      // only when MAGIC wrapped negative
///   q >>= shift;                 // arithmetic, only when shift > 0
///   x / d  =>  q + (q >>> 31)
///   Examples:
///   x / 6  =>  ((int)((long)x * 715827883 >> 32) + ((int)((long)x * 715827883 >> 32) >>> 31))
/// </summary>
public class DivideGranlundMontgomerySignedStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
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
		    || (d & d - 1) == 0 // power of 2 — already handled by DivideByPowerOfTwoToShiftStrategy
		    || !IsPure(context.Left.Syntax)) // x appears multiple times in the generated expression
		{
			return false;
		}

		optimized = GranlundMontgomeryEmitter.BuildSignedQuotient(context.Left.Syntax, d);
		return true;
	}
}