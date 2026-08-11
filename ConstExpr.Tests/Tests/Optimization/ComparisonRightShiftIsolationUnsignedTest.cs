namespace ConstExpr.Tests.Optimization;

/// <summary>
///   The right-shift isolation strategy is gated to Int32/UInt32/Int64/UInt64 — the only types a
///   shift expression's own result can have in C#. This exercises the unsigned side of that gate.
/// </summary>
[InheritsTests]
public class ComparisonRightShiftIsolationUnsignedTest : BaseTestWithRandomValues<Func<uint, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x >> 2 < 5, x >> 2 > 5, x >> 2 <= 5, x >> 2 >= 5));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 20u, x >= 24u, x < 24u, x >= 20u)),
	];
}