namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same overflow scenario as <see cref="RightShiftCombineOverflowSignedTest" />, but on an
///   unsigned operand where &gt;&gt; is logical (zero-fill), so the boundary result is the
///   literal 0 instead of a sign-saturating shift.
/// </summary>
[InheritsTests]
public class RightShiftCombineOverflowUnsignedTest : BaseTestWithRandomValues<Func<uint, uint>>
{
	public override string TestMethod => GetString(x => x >> 20 >> 20);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0u),
	];
}
