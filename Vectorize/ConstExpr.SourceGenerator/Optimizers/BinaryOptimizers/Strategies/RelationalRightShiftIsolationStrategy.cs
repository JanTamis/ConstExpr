using System.Numerics;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

/// <summary>
///   Isolates a variable operand right-shifted by a compile-time constant across a comparison:
///   <c>v &gt;&gt; c OP k</c>. Unlike <see cref="RelationalVariableIsolationStrategy" />'s multiply/add
///   family, a shift's implied divisor <c>2^c</c> is always positive, so there is never an operator
///   flip — but <c>&gt;&gt;</c> is a floor division (it rounds toward negative infinity, including for
///   negative signed values — <c>-3 &gt;&gt; 1 == -2</c>, not <c>-1</c>), so each operator needs its own
///   threshold adjustment rather than a single shared formula:
///   <code>
///     v &gt;&gt; c &lt;  k  &lt;=&gt;  v &lt;  (k * 2^c)
///     v &gt;&gt; c &gt;= k  &lt;=&gt;  v &gt;= (k * 2^c)
///     v &gt;&gt; c &lt;= k  &lt;=&gt;  v &lt;  ((k + 1) * 2^c)
///     v &gt;&gt; c &gt;  k  &lt;=&gt;  v &gt;= ((k + 1) * 2^c)
///   </code>
///   <paramref name="resultKind" /> is the operator the isolated form always uses (<c>&lt;</c> for the
///   less-than family, <c>&gt;=</c> for the greater-or-equal family); <paramref name="addOneToK" />
///   selects which of the two rows in that family applies.
///   <para>
///     <c>k</c> must be a bare <see cref="LiteralExpressionSyntax" /> — this strategy's
///     <c>TRight</c> only matches that shape, not one wrapped in a unary minus — so a negative
///     threshold (e.g. <c>v &gt;&gt; c &lt; -3</c>) never reaches <see cref="TryOptimize" /> at all
///     and silently isn't optimized; only the concrete value of <c>v</c> can be negative.
///   </para>
///   <para>
///     <c>==</c>/<c>!=</c> are deliberately not covered: <c>v &gt;&gt; c == k</c> is a half-open range
///     (<c>k * 2^c &lt;= v &lt; (k + 1) * 2^c</c>), not a single threshold, and this strategy's shape —
///     one <see cref="BinaryExpressionSyntax" /> result — can't express a conjunction. Left shift
///     (<c>&lt;&lt;</c>) is excluded for a different reason: <c>v &lt;&lt; c</c> truncates to the
///     operand's bit width, so <c>v &lt;&lt; c OP k</c> is only equivalent to <c>v OP k &gt;&gt; c</c>
///     when the shift didn't overflow, and this strategy has no proof that it didn't.
///   </para>
///   <para>
///     Requires only <see cref="FastMathFlags.Strict" />: unlike float/double reassociation, this
///     transform is exact for every representable operand — there is no rounding or reordering risk.
///   </para>
///   <para>
///     Gated to <see cref="SpecialType.System_Int32" />, <see cref="SpecialType.System_UInt32" />,
///     <see cref="SpecialType.System_Int64" />, and <see cref="SpecialType.System_UInt64" /> — the only
///     types a shift expression's own result can have in C# (byte/sbyte/short/ushort operands are
///     promoted to <c>int</c> by the shift operator itself). The shift amount must be a literal in
///     <c>[0, bit width)</c> — anything else either isn't a compile-time constant here or would itself
///     be masked by the runtime shift, which this strategy doesn't attempt to reproduce. The new
///     threshold is computed in <see cref="BigInteger" /> and range-checked against the operand type
///     before being emitted, so a threshold that would overflow is simply left unoptimized rather than
///     silently wrapping.
///   </para>
/// </summary>
public abstract class RelationalRightShiftIsolationStrategy(SyntaxKind resultKind, bool addOneToK)
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

		if (RemoveParentheses(context.Left.Syntax) is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.RightShiftExpression } shift
		    || shift.Right is not LiteralExpressionSyntax shiftAmountLiteral)
		{
			return false;
		}

		var bitWidth = specialType is SpecialType.System_Int32 or SpecialType.System_UInt32 ? 32 : 64;

		if (!TryGetShiftAmount(shiftAmountLiteral, bitWidth, out var shiftAmount)
		    || ToBigInteger(context.Right.Syntax.Token.Value.ToSpecialType(specialType.Value)) is not { } k)
		{
			return false;
		}

		var threshold = (addOneToK ? k + 1 : k) * (BigInteger.One << shiftAmount);

		if (!TryFromBigInteger(threshold, specialType.Value, out var thresholdValue))
		{
			return false;
		}

		optimized = BinaryExpression(resultKind, shift.Left, CreateLiteral(thresholdValue));
		return true;
	}

	/// <summary>
	///   The shift amount is read straight off the literal's own boxed value rather than normalized via
	///   <see cref="ObjectExtensions.ToSpecialType{T}" /> to the shifted operand's type first: shift
	///   counts in source are ordinary <c>int</c> literals regardless of the shifted value's type (e.g.
	///   <c>someLong &gt;&gt; 2</c>), so normalizing to the operand's (possibly 64-bit) type first would
	///   be pointless — only the range check against <paramref name="bitWidth" /> matters here.
	/// </summary>
	private static bool TryGetShiftAmount(LiteralExpressionSyntax literal, int bitWidth, out int shiftAmount)
	{
		shiftAmount = 0;

		if (ToBigInteger(literal.Token.Value) is not { } value || value < 0 || value >= bitWidth)
		{
			return false;
		}

		shiftAmount = (int) value;
		return true;
	}
}