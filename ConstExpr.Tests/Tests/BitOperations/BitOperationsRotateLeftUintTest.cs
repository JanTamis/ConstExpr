namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateLeftUintTest : BaseTest<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateLeft(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 8U, [ 1u, 3 ]),
		Create((_, _) => 2147483648U, [ 1u, 31 ]),
		Create((_, _) => 1U, [ 1u, 0 ])
	];
}