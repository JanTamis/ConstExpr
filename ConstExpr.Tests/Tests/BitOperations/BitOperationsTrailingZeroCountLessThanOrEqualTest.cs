namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies the &lt;= dual of <see cref="BitOperationsTrailingZeroCountGreaterThanTest" />:
///   TrailingZeroCount(x) &lt;= 2 =&gt; (x &amp; 7U) != 0.
/// </summary>
[InheritsTests]
public class BitOperationsTrailingZeroCountLessThanOrEqualTest : BaseTestWithRandomValues<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.TrailingZeroCount(x) <= 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 7U) != 0),
	];
}
