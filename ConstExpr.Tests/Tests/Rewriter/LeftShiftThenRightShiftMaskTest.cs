namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for high-bit clearing on unsigned operands: (x &lt;&lt; c) &gt;&gt; c =>
///   x &amp; ((1 &lt;&lt; (bitWidth - c)) - 1).
/// </summary>
[InheritsTests]
public class LeftShiftThenRightShiftMaskTest : BaseTestWithRandomValues<Func<uint, uint>>
{
	public override string TestMethod => GetString(x => x << 4 >> 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & 268435455u)
	];
}