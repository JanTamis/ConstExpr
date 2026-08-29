namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   (uint)x * 2 → x &lt;&lt; 1. Companion to <see cref="MultiplyByTwoTest" /> (signed int):
///   MultiplyByTwoToShiftStrategy must route both signedness classes through the same
///   shift reduction. Locks in the base-type gate so a later change to
///   UnsigedIntegerBinaryStrategy / IntegerBinaryStrategy can't silently drop one.
/// </summary>
[InheritsTests]
public class MultiplyByTwoUnsignedTest : BaseTestWithRandomValues<Func<uint, uint>>
{
	public override string TestMethod => GetString(x => x * 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x << 1)
	];
}