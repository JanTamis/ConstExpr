namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same as <see cref="LeftShiftCombineOverflowTest" /> but for a 64-bit operand, confirming
///   the bit-width lookup isn't hardcoded to 32.
/// </summary>
[InheritsTests]
public class LeftShiftCombineOverflowLongTest : BaseTestWithRandomValues<Func<long, long>>
{
	public override string TestMethod => GetString(x => x << 40 << 40);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0L),
	];
}
