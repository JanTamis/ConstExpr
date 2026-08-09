namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateRightUintTest : BaseTest<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateRight(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(8u, 3),
		CreateFolded(1u, 29)
	];
}