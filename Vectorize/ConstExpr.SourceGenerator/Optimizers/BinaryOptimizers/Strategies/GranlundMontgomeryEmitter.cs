using Microsoft.CodeAnalysis.CSharp;
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

		// q0 = (uint)((ulong)x * MAGIC >> 32)
		ExpressionSyntax BuildQ0() => CreateCastSyntax<uint>(ParenthesizedExpression(
			RightShiftExpression(
				MultiplyExpression(CreateCastSyntax<ulong>(x), CreateLiteral((ulong) magic)),
				CreateLiteral(32))));

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
	}

	/// <summary>Builds the quotient of <c>x / d</c> for a signed 32-bit dividend (with <c>d &gt; 1</c>).</summary>
	public static ExpressionSyntax BuildSignedQuotient(ExpressionSyntax x, int d)
	{
		GranlundMontgomery.ComputeSigned(d, out var magic, out var shift);

		// q = (int)((long)x * MAGIC >> 32), then optional "+ x" and arithmetic ">> shift".
		// The final ">> shift" is left WITHOUT an enclosing parenthesis so the base can be
		// reused verbatim inside the sign-bit shift `q >>> 31` (where `>>` and `>>>` share
		// precedence and associativity, so no parenthesis is required).
		ExpressionSyntax BuildBase()
		{
			ExpressionSyntax q = CreateCastSyntax<int>(ParenthesizedExpression(
				RightShiftExpression(
					MultiplyExpression(CreateCastSyntax<long>(x), CreateLiteral(magic)),
					CreateLiteral(32))));

			// magic wrapped negative for a positive divisor — add back the dividend
			if (magic < 0)
			{
				q = ParenthesizedExpression(AddExpression(q, x));
			}

			if (shift > 0)
			{
				q = RightShiftExpression(q, CreateLiteral(shift));
			}

			return q;
		}

		// (q >>> 31) — sign-bit correction (truncation toward zero for negative dividends).
		var signBit = ParenthesizedExpression(BinaryExpression(SyntaxKind.UnsignedRightShiftExpression, BuildBase(), CreateLiteral(31)));

		// q + (q >>> 31). The left operand must be parenthesized when it is a shift expression,
		// since `+` binds tighter than `>>` (e.g. `a >> b + c` parses as `a >> (b + c)`).
		var left = BuildBase();

		if (shift > 0)
		{
			left = ParenthesizedExpression(left);
		}

		return ParenthesizedExpression(AddExpression(left, signBit));
	}
}