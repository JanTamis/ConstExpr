namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies that a TrailingZeroCount upper-bound test collapses to a mask check:
///   TrailingZeroCount(x) &lt; 3 =&gt; (x &amp; 7U) != 0. The negation of
///   <see cref="BitOperationsTrailingZeroCountCompareTest" />'s &gt;= fold.
/// </summary>
[InheritsTests]
public class BitOperationsTrailingZeroCountLessThanTest : BaseTestWithRandomValues<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.TrailingZeroCount(x) < 3);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 7U) != 0),
	];
}
