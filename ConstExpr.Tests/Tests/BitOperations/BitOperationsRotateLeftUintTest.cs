namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateLeftUintTest : BaseTest<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateLeft(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(1u, 3),
		CreateFolded(1u, 31),
		CreateFolded(1u, 0)
	];
}