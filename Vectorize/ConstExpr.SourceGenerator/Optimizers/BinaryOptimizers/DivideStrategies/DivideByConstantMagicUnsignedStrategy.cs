using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Magic-number ("multiply-high") division for unsigned 32-bit division by a constant
///   non-power-of-2 divisor:
///   x / d  =>  (uint)((ulong)x * MAGIC >> SHIFT)
///   Replaces the hardware division instruction with a multiply and a shift.
///   This is the quotient counterpart of <see cref="ModuloStrategies.ModuloBarrettUnsignedStrategy" />,
///   which computes exactly this <c>q = (uint)((ulong)x * MAGIC >> SHIFT)</c> internally before
///   returning <c>x - d * q</c>. Because the Barrett condition guarantees <c>q == floor(x / d)</c>
///   for every <see cref="uint" /> <c>x</c>, emitting <c>q</c> on its own is an exact division.
///   The magic number and shift satisfy:
///   magic = ceil(2^shift / d) fits in uint32, and (magic * d - 2^shift) * uint.MaxValue &lt; 2^shift
///   which guarantees the quotient is exact for all uint x.
///   Examples:
///   x / 3u  =>  (uint)((ulong)x * 2863311531UL >> 33)
///   x / 5u  =>  (uint)((ulong)x * 3435973837UL >> 34)
/// </summary>
public class DivideByConstantMagicUnsignedStrategy : UnsigedIntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
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
		    || !TryComputeMagic(d, out var magic, out var shift))
		{
			return false;
		}

		var x = context.Left.Syntax;

		// (ulong)x * MAGIC >> SHIFT
		var innerMul = MultiplyExpression(CreateCastSyntax<ulong>(x), CreateLiteral(magic));
		var shifted = RightShiftExpression(innerMul, CreateLiteral(shift));

		// (uint)((ulong)x * MAGIC >> SHIFT) — the exact quotient floor(x / d)
		optimized = CreateCastSyntax<uint>(ParenthesizedExpression(shifted));
		return true;
	}

	/// <summary>
	///   Finds the smallest shift >= 32 such that magic = ceil(2^shift / d) fits in uint32
	///   and the Barrett condition holds: (magic*d - 2^shift) * uint.MaxValue &lt; 2^shift.
	///   Identical to <see cref="ModuloStrategies.ModuloBarrettUnsignedStrategy" /> so that the
	///   emitted quotient matches the value that strategy relies on.
	/// </summary>
	private static bool TryComputeMagic(uint d, out ulong magic, out int shift)
	{
		magic = 0;
		shift = 0;

		for (var sh = 32; sh <= 63; sh++)
		{
			var pow = 1UL << sh;
			var m = (pow + d - 1) / d; // ceil(2^sh / d)

			if (m > UInt32.MaxValue)
			{
				continue;
			}

			var e = m * d - pow; // 0 <= e < d (from ceiling property)

			if (e == 0 || e * UInt32.MaxValue < pow)
			{
				magic = m;
				shift = sh;
				return true;
			}
		}

		return false;
	}
}