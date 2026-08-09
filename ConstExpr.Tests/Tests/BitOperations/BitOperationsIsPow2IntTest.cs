namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsIsPow2IntTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}