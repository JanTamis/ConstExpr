namespace ConstExpr.Tests.Rewriter;

/// <summary>64-bit coverage for LeftShiftThenRightShiftMaskStrategy: (x &lt;&lt; c) &gt;&gt; c on ulong.</summary>
[InheritsTests]
public class LeftShiftThenRightShiftMaskUlongTest : BaseTestWithRandomValues<Func<ulong, ulong>>
{
	public override string TestMethod => GetString(x => x << 4 >> 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & 1152921504606846975ul)
	];
}