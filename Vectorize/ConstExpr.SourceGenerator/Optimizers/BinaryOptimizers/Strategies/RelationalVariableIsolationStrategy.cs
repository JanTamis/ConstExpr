using System;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Isolates a variable operand combined with a compile-time constant across a comparison:
///   <c>v * c OP k</c> becomes <c>v OP k / c</c>; <c>v / c OP k</c> becomes <c>v OP k * c</c> (both
///   flipping <c>OP</c> when <c>c</c> is negative); <c>v + c OP k</c> becomes <c>v OP k - c</c>;
///   <c>v - c OP k</c> becomes <c>v OP k + c</c>; <c>c - v OP k</c> becomes <c>v OP' c - k</c> (always
///   flips: the coefficient of <c>v</c> is -1). <c>c * v</c>, <c>c + v</c>, <c>-(v * c)</c>,
///   <c>-(v / c)</c>, and (for <c>c = 2</c>) <c>v + v</c> are recognized too, matching what the
///   upstream multiply/add strategies canonicalize each shape to. <c>c / v</c> is deliberately NOT
///   recognized — that's a reciprocal, not a linear term: whether the comparison needs to flip would
///   depend on the sign of <c>v</c> itself, not just of <c>c</c>, so it can't be isolated the same way.
///   For
///   <c>OP</c> in <c>{==, !=}</c> there is nothing to flip — <paramref name="unflippedKind" /> and
///   <paramref name="flippedKind" /> are simply passed the same value by those subclasses, so the
///   flip decision below becomes a no-op rather than needing a separate code path.
///   <para>
///     A compound left side composes for free: isolating one layer can leave a
///     <see cref="BinaryExpressionSyntax" /> result (e.g. <c>(v + 3) &lt; 5</c> from
///     <c>(v + 3) * 2 &lt; 10</c>), and <c>TryOptimizeNode</c>'s existing re-optimization step already
///     re-runs the same strategies on that result — so <c>(v + 3) * 2 &lt; 10</c> reaches <c>v &lt; 2</c>
///     without this strategy needing to parse multi-term affine expressions itself.
///   </para>
///   <para>
///     Requires <see cref="FastMathFlags.AssociativeMath" /> for all three operators alike: none of
///     <c>k / c</c>, <c>k - c</c>, or <c>k + c</c> is a computation the source wrote, and for
///     float/double none is guaranteed exact — e.g. <c>v * 6F &lt; 1F</c> and <c>v &lt; 1F / 6F</c> can
///     disagree for values of <c>v</c> right at the boundary the comparison guards, and the same
///     reassociation risk applies to moving an added/subtracted term across the comparison. Restricted
///     to <see cref="SpecialType.System_Single" /> and <see cref="SpecialType.System_Double" />:
///     integer division would change which values satisfy the comparison (not just round them) and
///     integer add/subtract would need an overflow proof this strategy doesn't attempt, while decimal
///     rounds in base 10 and can throw <see cref="OverflowException" /> — none of that is the "may
///     differ from strict IEEE 754" tradeoff this flag is meant to cover.
///   </para>
/// </summary>
public abstract class RelationalVariableIsolationStrategy(SyntaxKind unflippedKind, SyntaxKind flippedKind)
	: BaseBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!context.Left.Type.IsFloatingNumeric())
		{
			return false;
		}

		return TryIsolateViaMultiplicative(context, out optimized) || TryIsolateViaAddition(context, out optimized);
	}

	/// <summary>
	///   Handles <c>v * c OP k</c>, <c>v / c OP k</c>, <c>c * v OP k</c>, <c>-(v * c) OP k</c>,
	///   <c>-(v / c) OP k</c>, and (for <c>c = 2</c>) <c>v + v OP k</c> via
	///   <see cref="TryGetMultiplicativeCoefficient" />.
	/// </summary>
	private bool TryIsolateViaMultiplicative(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!TryGetMultiplicativeCoefficient(context, out var variable, out var rawCoefficient, out var negatedByUnary, out var isDivide))
		{
			return false;
		}

		var specialType = context.Left.Type!.SpecialType;
		var coefficient = rawCoefficient.ToSpecialType(specialType);

		// Fold the wrapping -(v * c)/-(v / c), if any, into the coefficient's own sign here, once, so
		// the new threshold and the flip decision both read off the same signed value — computing the
		// threshold from the unsigned literal and flipping the operator separately would isolate the
		// right magnitude with the wrong sign.
		if (negatedByUnary)
		{
			coefficient = coefficient.Negate();
		}

		if (coefficient is null || coefficient.IsNumericZero() || !(coefficient.IsPositive() || coefficient.IsNegative()))
		{
			// Zero (should already be folded upstream by MultiplyByZeroStrategy, or is a v / 0 that
			// isn't this strategy's business to touch), NaN, or some other non-plain numeric literal
			// value — nothing safe to isolate.
			return false;
		}

		var k = context.Right.Syntax.Token.Value.ToSpecialType(specialType);
		// v * c OP k  =>  v OP k / c        v / c OP k  =>  v OP k * c
		var newThreshold = isDivide ? k.Multiply(coefficient) : k.Divide(coefficient);

		if (newThreshold is null || !IsFinite(newThreshold))
		{
			return false;
		}

		optimized = BinaryExpression(coefficient.IsNegative() ? flippedKind : unflippedKind, variable, CreateLiteral(newThreshold));
		return true;
	}

	/// <summary>
	///   Peels the canonical <c>v * c</c>/<c>c * v</c>/<c>v / c</c> shape — or, for <c>c = 2</c>, the
	///   <c>v + v</c> shape <see cref="MultiplyStrategies.MultiplyByTwoToAdditionStrategy" />
	///   canonicalizes it to — optionally wrapped in a negating <c>-(...)</c>, off
	///   <c>context.Left.Syntax</c>. Doesn't interpret <paramref name="coefficient" />'s sign: the
	///   caller combines it with <paramref name="negatedByUnary" /> in one place. <c>c / v</c> (literal
	///   numerator) is intentionally not matched — see the class-level remarks.
	/// </summary>
	private bool TryGetMultiplicativeCoefficient(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax variable, out object? coefficient, out bool negatedByUnary, out bool isDivide)
	{
		variable = null!;
		coefficient = null;
		negatedByUnary = false;
		isDivide = false;

		var expr = RemoveParentheses(context.Left.Syntax);

		if (expr is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } unary)
		{
			negatedByUnary = true;
			expr = RemoveParentheses(unary.Operand);
		}

		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } multiply)
		{
			if (multiply.Right is LiteralExpressionSyntax rightLiteral)
			{
				variable = multiply.Left;
				coefficient = rightLiteral.Token.Value;
				return true;
			}

			if (multiply.Left is LiteralExpressionSyntax leftLiteral)
			{
				variable = multiply.Right;
				coefficient = leftLiteral.Token.Value;
				return true;
			}

			return false;
		}

		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } divide
		    && divide.Right is LiteralExpressionSyntax divisorLiteral)
		{
			variable = divide.Left;
			coefficient = divisorLiteral.Token.Value;
			isDivide = true;
			return true;
		}

		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression } add
		    && IsPure(add.Left)
		    && LeftEqualsRight(add.Left, add.Right, context.Variables))
		{
			variable = add.Left;
			coefficient = 2;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Handles <c>v + c OP k</c>, <c>c + v OP k</c>, <c>v - c OP k</c>, and <c>c - v OP k</c>. Not
	///   attempted under an outer unary minus — that shape belongs to
	///   <see cref="TryIsolateViaMultiplicative" /> (<c>-(v * c)</c>/<c>-(v / c)</c>), and
	///   <c>-(v + c)</c>/<c>-(v - c)</c> are not canonical output of any
	///   upstream pass, so there is nothing to recognize for them here.
	/// </summary>
	private bool TryIsolateViaAddition(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (RemoveParentheses(context.Left.Syntax) is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression or (int) SyntaxKind.SubtractExpression } additive)
		{
			return false;
		}

		var specialType = context.Left.Type!.SpecialType;
		var k = context.Right.Syntax.Token.Value.ToSpecialType(specialType);
		var isAdd = additive.IsKind(SyntaxKind.AddExpression);

		ExpressionSyntax variable;
		object? newThreshold;
		var flip = false;

		if (additive.Right is LiteralExpressionSyntax rightLiteral)
		{
			// v + c OP k  =>  v OP k - c
			// v - c OP k  =>  v OP k + c
			var offset = rightLiteral.Token.Value.ToSpecialType(specialType);
			variable = additive.Left;
			newThreshold = isAdd ? k.Subtract(offset) : k.Add(offset);
		}
		else
			switch (isAdd)
			{
				case true when additive.Left is LiteralExpressionSyntax leftLiteral:
				{
					// c + v OP k  =>  v OP k - c
					var offset = leftLiteral.Token.Value.ToSpecialType(specialType);
					variable = additive.Right;
					newThreshold = k.Subtract(offset);
					break;
				}
				case false when additive.Left is LiteralExpressionSyntax subLeftLiteral:
				{
					// c - v OP k  =>  v OP' c - k   (the coefficient of v is -1: always flips)
					var c = subLeftLiteral.Token.Value.ToSpecialType(specialType);
					variable = additive.Right;
					newThreshold = c.Subtract(k);
					flip = true;
					break;
				}
				default:
					return false;
			}

		if (newThreshold is null || !IsFinite(newThreshold))
		{
			return false;
		}

		optimized = BinaryExpression(flip ? flippedKind : unflippedKind, variable, CreateLiteral(newThreshold));
		return true;
	}

	private static bool IsFinite(object value)
	{
		return value switch
		{
			float f => !Single.IsNaN(f) && !Single.IsInfinity(f),
			double d => !Double.IsNaN(d) && !Double.IsInfinity(d),
			_ => false
		};
	}
}