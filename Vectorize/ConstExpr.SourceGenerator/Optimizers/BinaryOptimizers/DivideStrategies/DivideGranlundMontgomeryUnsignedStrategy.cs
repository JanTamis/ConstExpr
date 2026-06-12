using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Granlund-Montgomery division for unsigned 32-bit division by a constant non-power-of-2
///   divisor: replaces the division instruction with a multiply-high + shift sequence.
///   No "add" fixup needed:
///   x / d  =>  (q0 >> shift)
///   "add" fixup needed (magic does not fit in 32 bits):
///   x / d  =>  (q0 + (x - q0 >> 1) >> (shift - 1))
///   where q0 = (uint)((ulong)x * MAGIC >> 32).
///   Examples:
///   x / 7u  =>  (q0 + (x - q0 >> 1) >> 2)   with MAGIC = 613566757
/// </summary>
public class DivideGranlundMontgomeryUnsignedStrategy : UnsigedIntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
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
		    || (d & d - 1) == 0 // power of 2 — already handled by DivideByPowerOfTwoToShiftStrategy
		    || !IsPure(context.Left.Syntax)) // x appears multiple times in the generated expression
		{
			return false;
		}

		optimized = GranlundMontgomeryEmitter.BuildUnsignedQuotient(context.Left.Syntax, d);
		return true;
	}
}