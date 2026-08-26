namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Shift counts that mod down to 0 against the bit width (here 32) fold straight to the
///   operand instead of a wasteful "x &amp; -1": (x &gt;&gt; 32) &lt;&lt; 32 => x.
/// </summary>
[InheritsTests]
public class RightShiftThenLeftShiftMaskZeroModTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x >> 32 << 32);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x)
	];
}