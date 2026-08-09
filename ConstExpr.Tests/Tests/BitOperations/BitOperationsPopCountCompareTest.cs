namespace ConstExpr.Tests.BitOperations;

/// <summary>
///   Verifies PopCount comparison strategies:
///   PopCount(x) == 1 => IsPow2(x), == 0 => x == 0, and the != mirrors.
/// </summary>
[InheritsTests]
public class BitOperationsPopCountCompareTest : BaseTestWithRandomValues<Func<uint, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (
		System.Numerics.BitOperations.PopCount(x) == 1,
		System.Numerics.BitOperations.PopCount(x) == 0,
		System.Numerics.BitOperations.PopCount(x) != 1,
		System.Numerics.BitOperations.PopCount(x) != 0));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var bitOperationsIsPow2 = System.Numerics.BitOperations.IsPow2(x);

			return (bitOperationsIsPow2, x == 0, !bitOperationsIsPow2, x != 0);
		}),
	];
}