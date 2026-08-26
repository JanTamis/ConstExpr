namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for high-bit clearing via '&gt;&gt;&gt;', which is always logical regardless of
///   signedness: (x &lt;&lt; c) &gt;&gt;&gt; c => x &amp; ((1 &lt;&lt; (bitWidth - c)) - 1). Uses a signed int
///   operand to demonstrate this holds even where the plain '&gt;&gt;' mirror would decline.
/// </summary>
[InheritsTests]
public class LeftShiftThenUnsignedRightShiftMaskTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x << 4 >>> 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & 268435455)
	];
}