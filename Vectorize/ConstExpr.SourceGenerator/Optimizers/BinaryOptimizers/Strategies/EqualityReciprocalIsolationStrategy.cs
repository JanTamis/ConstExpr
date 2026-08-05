using System;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Isolates a variable operand in a reciprocal position across an equality comparison:
///   <c>c / v == k</c> becomes <c>v == c / k</c>. This is the one form
///   <see cref="RelationalVariableIsolationStrategy" /> deliberately does not handle (documented there
///   as "c / v is deliberately NOT recognized" — for inequalities, the reciprocal's monotonicity
///   depends on the sign of v itself). Equality doesn't care about ordering, only which single value of
///   v (if any) satisfies the equation, so the reciprocal relation is safe here even though it isn't for
///   <c>&lt;</c>/<c>&gt;</c>/<c>&lt;=</c>/<c>&gt;=</c>.
///   <para>
///     Requires <see cref="FastMathFlags.AssociativeMath" />, matching
///     <see cref="RelationalVariableIsolationStrategy" />'s own float/double family: <c>c / k</c> is a
///     division the source never wrote, and isn't guaranteed to round-trip back through <c>c / v</c>
///     bit-exactly for every representable value.
///   </para>
///   <para>
///     Declines when <c>c == 0</c> or <c>k == 0</c>: <c>0 / v == k</c> is just <c>k == 0</c> for every
///     <c>v != 0</c> (not a single-value isolation), and <c>c / v == 0</c> is impossible for any finite
///     v when <c>c != 0</c> (no threshold to isolate either) — both are degenerate rather than unsafe,
///     but neither reduces to a single <c>v == threshold</c>.
///   </para>
///   <para>
///     Restricted to <see cref="SpecialType.System_Single" /> and <see cref="SpecialType.System_Double" />
///     — same reasoning as <see cref="RelationalVariableIsolationStrategy" />: integer division is a
///     truncating range (already excluded, in <see cref="EqualityIntegerIsolationStrategy" /> too, for
///     that reason), and decimal can throw <see cref="OverflowException" />.
///   </para>
/// </summary>
public abstract class EqualityReciprocalIsolationStrategy(SyntaxKind kind)
	: BaseBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		var specialType = context.Left.Type?.SpecialType;

		if (specialType is not (SpecialType.System_Single or SpecialType.System_Double))
		{
			return false;
		}

		if (RemoveParentheses(context.Left.Syntax) is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } divide)
		{
			return false;
		}

		if (divide.Right is LiteralExpressionSyntax || divide.Left is not LiteralExpressionSyntax literalC)
		{
			// divide.Right being a literal is the forward v / c shape — RelationalVariableIsolationStrategy's job, not this one.
			return false;
		}

		var c = literalC.Token.Value.ToSpecialType(specialType.Value);
		var k = context.Right.Syntax.Token.Value.ToSpecialType(specialType.Value);

		if (c is null || k is null || c.IsNumericZero() || k.IsNumericZero())
		{
			return false;
		}

		var threshold = c.Divide(k);

		if (threshold is null || !IsFinite(threshold))
		{
			return false;
		}

		optimized = BinaryExpression(kind, divide.Right, CreateLiteral(threshold));
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