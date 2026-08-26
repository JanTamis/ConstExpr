namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for low-bit clearing: (x &gt;&gt; c) &lt;&lt; c => x &amp; ~((1 &lt;&lt; c) - 1).</summary>
[InheritsTests]
public class RightShiftThenLeftShiftMaskTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x >> 4 << 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & -16)
	];
}