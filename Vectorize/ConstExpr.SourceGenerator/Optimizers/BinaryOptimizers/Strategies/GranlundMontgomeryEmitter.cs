using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Emits the multiply-high + shift quotient expression for <c>x / d</c> using the
///   <see cref="GranlundMontgomery" /> magic numbers. Shared by the division and modulo
///   strategies so <c>x / d</c> and <c>x % d</c> stay consistent (modulo wraps the
///   quotient as <c>x - d * quotient</c>).
///   The dividend <paramref name="x" /> is referenced multiple times, so callers must
///   ensure it is a pure expression before invoking these builders.
/// </summary>
internal static class GranlundMontgomeryEmitter
{
	/// <summary>Builds the quotient of <c>x / d</c> for an unsigned 32-bit dividend.</summary>
	public static ExpressionSyntax BuildUnsignedQuotient(ExpressionSyntax x, uint d)
	{
		GranlundMontgomery.ComputeUnsigned(d, out var magic, out var add, out var shift);

		if (add)
		{
			// (q0 + (x - q0 >> 1) >> (shift - 1))
			var halved = ParenthesizedExpression(RightShiftExpression(SubtractExpression(x, BuildQ0()), CreateLiteral(1)));
			var numerator = AddExpression(BuildQ0(), halved);

			return ParenthesizedExpression(RightShiftExpression(numerator, CreateLiteral(shift - 1)));
		}

		// (q0 >> shift)
		return shift == 0
			? BuildQ0()
			: ParenthesizedExpression(RightShiftExpression(BuildQ0(), CreateLiteral(shift)));

		// q0 = (uint)((ulong)x * MAGIC >> 32)
		ExpressionSyntax BuildQ0() => CreateCastSyntax<uint>(ParenthesizedExpression(
			RightShiftExpression(
				MultiplyExpression(x, CreateLiteral((ulong) magic)),
				CreateLiteral(32))));
	}

	/// <summary>Builds the quotient of <c>x / d</c> for a signed 32-bit dividend (with <c>d &gt; 1</c>).</summary>
	public static ExpressionSyntax BuildSignedQuotient(ExpressionSyntax x, int d)
	{
		GranlundMontgomery.ComputeSigned(d, out var magic, out var shift);

		// q = (int)((long)x * MAGIC >> 32)
		ExpressionSyntax q = CreateCastSyntax<int>(ParenthesizedExpression(
			RightShiftExpression(
				MultiplyExpression(x, CreateLiteral((long) magic)),
				CreateLiteral(32))));

		// magic wrapped negative for a positive divisor — add back the dividend
		if (magic < 0)
		{
			q = ParenthesizedExpression(AddExpression(q, x));
		}

		if (shift > 0)
		{
			// arithmetic ">> shift", parenthesized because `-` binds tighter than `>>`
			// (otherwise `q >> shift - (x >> 31)` would parse as `q >> (shift - (x >> 31))`).
			q = ParenthesizedExpression(RightShiftExpression(q, CreateLiteral(shift)));
		}

		// q - (x >> 31): truncation toward zero for negative dividends. Keying the correction
		// off the dividend's sign (x >> 31 == -1 when x < 0, else 0) keeps the multiply-high
		// base in the expression only once, versus re-reading the quotient as `q + (q >>> 31)`.
		// (x >> 31) is parenthesized since `-` binds tighter than `>>`.
		var correction = ParenthesizedExpression(RightShiftExpression(x, CreateLiteral(31)));

		return ParenthesizedExpression(SubtractExpression(q, correction));
	}
}