namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateRightUintTest : BaseTest<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateRight(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 1U, [ 8u, 3 ]),
		Create((_, _) => 8U, [ 1u, 29 ])
	];
}