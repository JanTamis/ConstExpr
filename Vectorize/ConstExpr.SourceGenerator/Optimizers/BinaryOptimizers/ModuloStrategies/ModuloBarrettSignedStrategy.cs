using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Barrett reduction for signed 32-bit modulo by a positive constant non-power-of-2 divisor.
/// x % d => x - d * ((int)((long)x * MAGIC >> SHIFT) - (x >> 31))
///
/// The subtraction of (x >> 31) corrects for C#'s truncation-toward-zero semantics:
///   x >> 31 == -1 when x &lt; 0, and 0 when x >= 0.
/// This adds 1 to the quotient for negative dividends, matching truncated division.
///
/// The magic number and shift satisfy:
///   (magic * d - 2^shift) * int.MaxValue &lt; 2^shift
/// with magic &lt; 2^31 (fits in a positive int32).
///
/// Examples:
///   x % 3  =>  x - 3 * ((int)((long)x * 1431655766L >> 32) - (x >> 31))
///   x % 5  =>  x - 5 * ((int)((long)x * 1717986919L >> 33) - (x >> 31))
///   x % 7  =>  x - 7 * ((int)((long)x * 1227133514L >> 33) - (x >> 31))
/// </summary>
public class ModuloBarrettSignedStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool IsValidSpecialType(SpecialType specialType) =>
		specialType == SpecialType.System_Int32;

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized))
			return false;

		if (context.Right.Syntax.Token.Value is not int d
		    || d <= 1
		    || (d & (d - 1)) == 0 // power of 2 — already handled by ModuloByPowerOfTwoStrategy
		    || !TryComputeMagic(d, out var magic, out var shift))
			return false;

		// x appears twice in the generated expression — only safe for pure operands
		if (!IsPure(context.Left.Syntax))
			return false;

		var x = context.Left.Syntax;

		// (long)x * MAGIC >> SHIFT
		var innerMul = MultiplyExpression(CreateCastSyntax<long>(x), CreateLiteral(magic));
		var shifted  = RightShiftExpression(innerMul, CreateLiteral(shift));

		// (int)((long)x * MAGIC >> SHIFT)
		var castInt = CreateCastSyntax<int>(ParenthesizedExpression(shifted));

		// (x >> 31)  — sign bit: -1 if x < 0, 0 if x >= 0
		var signBit = ParenthesizedExpression(RightShiftExpression(x, CreateLiteral(31)));

		// (int)(...) - (x >> 31)
		var quotient = SubtractExpression(castInt, signBit);

		// d * ((int)(...) - (x >> 31))
		var quotMul = MultiplyExpression(
			context.Right.Syntax,
			ParenthesizedExpression(quotient));

		// x - d * (...)
		optimized = SubtractExpression(x, quotMul);
		return true;
	}

	/// <summary>
	/// Finds the smallest shift in [32, 62] such that magic = ceil(2^shift / d) fits in int32
	/// and the Barrett condition holds: (magic*d - 2^shift) * int.MaxValue &lt; 2^shift.
	/// </summary>
	private static bool TryComputeMagic(int d, out long magic, out int shift)
	{
		magic = 0;
		shift = 0;

		for (var sh = 32; sh <= 62; sh++) // 1L << 63 would overflow long
		{
			var pow = 1L << sh;
			var m   = (pow + d - 1) / d; // ceil(2^sh / d)

			if (m > int.MaxValue) // magic must fit in a positive int32
				continue;

			var e = m * d - pow; // 0 <= e < d (from ceiling property)
			if (e == 0 || e * int.MaxValue < pow)
			{
				magic = m;
				shift = sh;
				return true;
			}
		}

		return false;
	}
}


