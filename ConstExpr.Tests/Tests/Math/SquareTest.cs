namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SquareTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => n * n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(5),
		CreateFolded(0),
		CreateFolded(-10)
	];
}