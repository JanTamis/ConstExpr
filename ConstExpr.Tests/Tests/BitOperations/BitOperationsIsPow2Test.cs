namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsIsPow2UintTest : BaseTest<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(8u),
		CreateFolded(1024u),
		CreateFolded(0u),
		CreateFolded(7u),
		CreateFolded(6u)
	];
}

[InheritsTests]
public class BitOperationsIsPow2IntTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(16),
		CreateFolded(0),
		CreateFolded(-4),
		CreateFolded(6)
	];
}