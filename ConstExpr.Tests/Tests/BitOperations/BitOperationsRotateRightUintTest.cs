namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateRightUintTest : BaseTestWithRandomValues<Func<uint, int, uint>>
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateRight(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}