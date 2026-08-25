namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies that a TrailingZeroCount strict lower-bound test collapses to a mask check via
///   the c + 1 dual: TrailingZeroCount(x) &gt; 2 =&gt; (x &amp; 7U) == 0 (equivalent to
///   TrailingZeroCount(x) &gt;= 3, same mask as <see cref="BitOperationsTrailingZeroCountCompareTest" />).
/// </summary>
[InheritsTests]
public class BitOperationsTrailingZeroCountGreaterThanTest : BaseTestWithRandomValues<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.TrailingZeroCount(x) > 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 7U) == 0),
	];
}
