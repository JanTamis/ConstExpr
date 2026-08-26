namespace ConstExpr.Tests.Rewriter;

/// <summary>64-bit coverage for RightShiftThenLeftShiftMaskStrategy: (x &gt;&gt; c) &lt;&lt; c on long.</summary>
[InheritsTests]
public class RightShiftThenLeftShiftMaskLongTest : BaseTestWithRandomValues<Func<long, long>>
{
	public override string TestMethod => GetString(x => x >> 8 << 8);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & -256L)
	];
}