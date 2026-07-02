namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies that a TrailingZeroCount lower-bound test collapses to a mask check:
///   TrailingZeroCount(x) &gt;= 3 => (x &amp; 7U) == 0.
/// </summary>
[InheritsTests]
public class BitOperationsTrailingZeroCountCompareTest : BaseTest<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.TrailingZeroCount(x) >= 3);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 7U) == 0),
		Create(_ => true, [ 8u ]),
		Create(_ => true, [ 0u ]),
		Create(_ => true, [ 64u ]),
		Create(_ => false, [ 4u ]),
		Create(_ => false, [ 7u ])
	];
}