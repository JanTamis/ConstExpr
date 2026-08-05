using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Isolates an integer variable operand combined with a compile-time constant across an
///   equality/inequality comparison: <c>v OP c == k</c>. Replaces the disabled
///   <c>EqualsComparisonSimplifierStrategy</c>, which keyed its inverse purely off the operator kind
///   and so got non-commutative operators wrong depending on which side the literal was on
///   (<c>v - c == k</c> isolates to <c>v == k + c</c>, but <c>c - v == k</c> isolates to
///   <c>v == c - k</c> — a different formula, not just a relabeling).
///   <para>
///     <c>+</c>/<c>-</c> are always sound for any <c>c</c>: translation by a fixed constant is a
///     bijection on <c>Z/2^n</c> regardless of parity, and computing the new threshold through
///     <see cref="ObjectExtensions.Add{T}" />/<see cref="ObjectExtensions.Subtract{T}" /> (which use
///     <em>unchecked</em> <c>Expression.Add</c>/<c>Expression.Subtract</c>) reproduces the exact same
///     wraparound the runtime would — e.g. <c>v + 5 == int.MinValue</c> isolates to
///     <c>v == int.MaxValue - 4</c>, and that boundary case is exact, not approximate.
///   </para>
///   <para>
///     <c>*</c> is NOT always sound, unlike <c>+</c>/<c>-</c>: multiplication by <c>c</c> mod <c>2^n</c>
///     is only a bijection when <c>c</c> is odd (coprime to the modulus). For even <c>c</c> the map is
///     many-to-one, so isolating can silently change the answer — concretely, <c>v * 2 == 6</c> is
///     <c>true</c> not only for <c>v == 3</c> but also for <c>v == int.MinValue + 3</c> (confirmed:
///     <c>(int.MinValue + 3) * 2</c> wraps to exactly <c>6</c>). So even <c>c</c> is declined outright.
///     For odd <c>c</c>, a solution always exists (via the modular inverse of <c>c</c>) — but computing
///     that inverse is more machinery than this strategy implements, so it only isolates when <c>k</c>
///     is <em>exactly</em> divisible by <c>c</c> in ordinary arithmetic (in which case the exact
///     quotient IS the unique modular solution too); when <c>k</c> isn't evenly divisible by an odd
///     <c>c</c>, a solution still exists somewhere, this strategy just declines to find it.
///   </para>
///   <para>
///     <c>/</c> and both shifts are excluded entirely: integer division and <c>&gt;&gt;</c> both floor,
///     so <c>v / c == k</c> and <c>v &gt;&gt; c == k</c> are half-open ranges, not a single threshold —
///     see <see cref="RelationalRightShiftIsolationStrategy" />'s remarks for the same reasoning applied
///     to inequalities. <c>&lt;&lt;</c> has the same overflow problem as it does for inequalities.
///   </para>
///   <para>
///     Requires only <see cref="FastMathFlags.Strict" />: every transform this strategy performs is
///     exact by construction (wraparound-consistent addition/subtraction, or a verified-exact
///     divisibility check for multiplication) — there is no float-style rounding risk to gate on.
///   </para>
/// </summary>
public abstract class EqualityIntegerIsolationStrategy(SyntaxKind kind)
	: BaseBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		var specialType = context.Left.Type?.SpecialType;

		if (specialType is not (SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64))
		{
			return false;
		}

		if (RemoveParentheses(context.Left.Syntax) is not BinaryExpressionSyntax
		    {
			    RawKind: (int) SyntaxKind.AddExpression or (int) SyntaxKind.SubtractExpression or (int) SyntaxKind.MultiplyExpression
		    } binary)
		{
			return false;
		}

		var k = context.Right.Syntax.Token.Value.ToSpecialType(specialType.Value);

		return binary.IsKind(SyntaxKind.MultiplyExpression)
			? TryIsolateViaMultiply(binary, k, specialType.Value, out optimized)
			: TryIsolateViaAdditive(binary, k, specialType.Value, out optimized);
	}

	private bool TryIsolateViaAdditive(BinaryExpressionSyntax additive, object? k, SpecialType specialType, out ExpressionSyntax? optimized)
	{
		optimized = null;

		var isAdd = additive.IsKind(SyntaxKind.AddExpression);

		ExpressionSyntax variable;
		object? threshold;

		if (additive.Right is LiteralExpressionSyntax rightLiteral)
		{
			// v + c == k  =>  v == k - c
			// v - c == k  =>  v == k + c
			var c = rightLiteral.Token.Value.ToSpecialType(specialType);
			variable = additive.Left;
			threshold = isAdd ? k.Subtract(c) : k.Add(c);
		}
		else if (isAdd && additive.Left is LiteralExpressionSyntax leftLiteral)
		{
			// c + v == k  =>  v == k - c
			var c = leftLiteral.Token.Value.ToSpecialType(specialType);
			variable = additive.Right;
			threshold = k.Subtract(c);
		}
		else if (!isAdd && additive.Left is LiteralExpressionSyntax subLeftLiteral)
		{
			// c - v == k  =>  v == c - k   (the coefficient of v is -1: a different formula than
			// v - c == k above, not merely a flipped one — this is the task_915264c7 fix)
			var c = subLeftLiteral.Token.Value.ToSpecialType(specialType);
			variable = additive.Right;
			threshold = c.Subtract(k);
		}
		else
		{
			return false;
		}

		if (threshold is null)
		{
			return false;
		}

		optimized = BinaryExpression(kind, variable, CreateLiteral(threshold));
		return true;
	}

	private bool TryIsolateViaMultiply(BinaryExpressionSyntax multiply, object? k, SpecialType specialType, out ExpressionSyntax? optimized)
	{
		optimized = null;

		ExpressionSyntax variable;
		object? cRaw;

		if (multiply.Right is LiteralExpressionSyntax rightLiteral)
		{
			variable = multiply.Left;
			cRaw = rightLiteral.Token.Value;
		}
		else if (multiply.Left is LiteralExpressionSyntax leftLiteral)
		{
			variable = multiply.Right;
			cRaw = leftLiteral.Token.Value;
		}
		else
		{
			return false;
		}

		var c = cRaw.ToSpecialType(specialType);

		if (c is null || c.IsNumericZero()
		              || ToBigInteger(c) is not { } cBig || ToBigInteger(k) is not { } kBig
		              || cBig.IsEven)
		{
			// Zero (should already be folded upstream), or an even coefficient — multiplication by an
			// even c mod 2^n isn't a bijection, so isolating would be unsound (see the class remarks).
			return false;
		}

		if (kBig % cBig != 0)
		{
			// Odd c always has a solution via the modular inverse of c, even here — this strategy just
			// doesn't compute it, so it declines rather than guessing.
			return false;
		}

		if (!TryFromBigInteger(kBig / cBig, specialType, out var quotient))
		{
			return false;
		}

		optimized = BinaryExpression(kind, variable, CreateLiteral(quotient));
		return true;
	}
}