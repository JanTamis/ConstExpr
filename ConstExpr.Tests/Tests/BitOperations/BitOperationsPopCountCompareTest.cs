namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies PopCount comparison strategies:
///   PopCount(x) == 1 => IsPow2(x), == 0 => x == 0, and the != mirrors.
/// </summary>
[InheritsTests]
public class BitOperationsPopCountCompareTest : BaseTest<Func<uint, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (
		System.Numerics.BitOperations.PopCount(x) == 1,
		System.Numerics.BitOperations.PopCount(x) == 0,
		System.Numerics.BitOperations.PopCount(x) != 1,
		System.Numerics.BitOperations.PopCount(x) != 0));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (
			System.Numerics.BitOperations.IsPow2(x),
			x == 0,
			!System.Numerics.BitOperations.IsPow2(x),
			x != 0)),
		Create(_ => (true, false, false, true), [ 8u ]),
		Create(_ => (false, true, true, false), [ 0u ]),
		Create(_ => (false, false, true, true), [ 7u ])
	];
}