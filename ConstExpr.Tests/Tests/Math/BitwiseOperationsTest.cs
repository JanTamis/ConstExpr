namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) => a & b | a ^ b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(12, 10),
		CreateFolded(8, 8),
		CreateFolded(5, 0)
	];
}