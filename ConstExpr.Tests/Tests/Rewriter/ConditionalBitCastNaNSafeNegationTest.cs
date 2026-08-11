using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x &lt; y ? 0 : 1, with floating-point operands under FastMathFlags.Strict, must NOT rewrite the
///   negated condition by flipping the relational operator (x &gt;= y): that flip disagrees with
///   `!(x &lt; y)` whenever x or y is NaN. Strict is what makes this meaningful — under the default
///   FastMathFlags.All (NoNaN included), flipping the operator is the intended, opted-in behavior.
/// </summary>
[InheritsTests]
public class ConditionalBitCastNaNSafeNegationTest() : BaseTestWithRandomValues<Func<double, double, int>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((x, y) => x < y ? 0 : 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Unsafe.BitCast<bool, byte>(x >= y);")
	];
}