using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Barrett reduction for unsigned 32-bit modulo by a constant non-power-of-2 divisor:
/// x % d => x - d * (uint)((ulong)x * MAGIC >> SHIFT)
/// Avoids the division instruction at runtime.
///
/// The magic number and shift satisfy:
///   (magic * d - 2^shift) * uint.MaxValue &lt; 2^shift
/// which guarantees the formula is exact for all uint x.
///
/// Examples:
///   x % 3u  =>  x - 3u * (uint)((ulong)x * 2863311531UL >> 33)
///   x % 5u  =>  x - 5u * (uint)((ulong)x * 3435973837UL >> 34)
///   x % 7u  =>  x - 7u * (uint)((ulong)x * 4908534053UL >> 35)
/// </summary>
public class ModuloBarrettUnsignedStrategy : UnsigedIntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool IsValidSpecialType(SpecialType specialType) =>
		specialType == SpecialType.System_UInt32;

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized))
			return false;

		if (context.Right.Syntax.Token.Value is not uint d
		    || d <= 1
		    || (d & (d - 1)) == 0 // power of 2 — already handled by ModuloByPowerOfTwoStrategy
		    || !TryComputeMagic(d, out var magic, out var shift))
			return false;

		// x appears twice in the generated expression — only safe for pure operands
		if (!IsPure(context.Left.Syntax))
			return false;

		var x = context.Left.Syntax;

		// (ulong)x * MAGIC >> SHIFT
		var innerMul = MultiplyExpression(CreateCastSyntax<ulong>(x), CreateLiteral(magic));
		var shifted  = RightShiftExpression(innerMul, CreateLiteral(shift));

		// d * (uint)((ulong)x * MAGIC >> SHIFT)
		var quotMul = MultiplyExpression(
			context.Right.Syntax,
			CreateCastSyntax<uint>(ParenthesizedExpression(shifted)));

		// x - d * (uint)(...)
		optimized = SubtractExpression(x, quotMul);
		return true;
	}

	/// <summary>
	/// Finds the smallest shift >= 32 such that magic = ceil(2^shift / d) fits in uint32
	/// and the Barrett condition holds: (magic*d - 2^shift) * uint.MaxValue &lt; 2^shift.
	/// </summary>
	private static bool TryComputeMagic(uint d, out ulong magic, out int shift)
	{
		magic = 0;
		shift = 0;

		for (var sh = 32; sh <= 63; sh++)
		{
			var pow = 1UL << sh;
			var m   = (pow + d - 1) / d; // ceil(2^sh / d)

			if (m > uint.MaxValue)
				continue;

			var e = m * d - pow; // 0 <= e < d (from ceiling property)
			if (e == 0 || e * uint.MaxValue < pow)
			{
				magic = m;
				shift = sh;
				return true;
			}
		}

		return false;
	}
}


