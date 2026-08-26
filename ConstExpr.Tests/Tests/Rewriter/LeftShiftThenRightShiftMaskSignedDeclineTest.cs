namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: plain '&gt;&gt;' sign-extends on signed types, so it does not reduce to a mask -
///   (x &lt;&lt; 4) &gt;&gt; 4 must be left untouched by LeftShiftThenRightShiftMaskStrategy on int.
/// </summary>
[InheritsTests]
public class LeftShiftThenRightShiftMaskSignedDeclineTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x << 4 >> 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x << 4 >> 4)
	];
}