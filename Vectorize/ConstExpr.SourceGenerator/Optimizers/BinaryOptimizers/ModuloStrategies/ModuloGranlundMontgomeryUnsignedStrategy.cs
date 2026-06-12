using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
///   Granlund-Montgomery reduction for unsigned 32-bit modulo by a constant non-power-of-2
///   divisor. The quotient is computed with a multiply-high + shift (no division
///   instruction) and the remainder is recovered as <c>x - d * (x / d)</c>.
///   No "add" fixup needed:
///   x % d  =>  x - (q0 >> shift) * d
///   "add" fixup needed (magic does not fit in 32 bits):
///   x % d  =>  x - (q0 + (x - q0 >> 1) >> (shift - 1)) * d
///   where q0 = (uint)((ulong)x * MAGIC >> 32).
///   Examples:
///   x % 7u  =>  x - (q0 + (x - q0 >> 1) >> 2) * 7U   with MAGIC = 613566757
/// </summary>
public class ModuloGranlundMontgomeryUnsignedStrategy : UnsigedIntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.MagicNumberDivision ];

	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType == SpecialType.System_UInt32;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		if (context.Right.Syntax.Token.Value is not uint d
		    || d <= 1
		    || (d & d - 1) == 0 // power of 2 — already handled by ModuloByPowerOfTwoStrategy
		    || !IsPure(context.Left.Syntax)) // x appears multiple times in the generated expression
		{
			return false;
		}

		var x = context.Left.Syntax;
		var quotient = GranlundMontgomeryEmitter.BuildUnsignedQuotient(x, d);

		// x - q * d
		optimized = SubtractExpression(x, MultiplyExpression(quotient, CreateLiteral(d)));
		return true;
	}
}