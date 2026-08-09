namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateLeftUintTest : BaseTestWithRandomValues<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateLeft(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}