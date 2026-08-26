namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   64-bit coverage for LeftShiftThenUnsignedRightShiftMaskStrategy on a signed long, showing
///   '&gt;&gt;&gt;' still reduces to a mask where the plain '&gt;&gt;' mirror would decline.
/// </summary>
[InheritsTests]
public class LeftShiftThenUnsignedRightShiftMaskLongTest : BaseTestWithRandomValues<Func<long, long>>
{
	public override string TestMethod => GetString(x => x << 4 >>> 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & 1152921504606846975L)
	];
}