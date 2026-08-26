namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: mismatched shift counts have no clean single-mask reduction, so
///   (x &gt;&gt; 3) &lt;&lt; 5 must be left untouched by RightShiftThenLeftShiftMaskStrategy.
/// </summary>
[InheritsTests]
public class RightShiftThenLeftShiftMaskMismatchTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x >> 3 << 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >> 3 << 5)
	];
}