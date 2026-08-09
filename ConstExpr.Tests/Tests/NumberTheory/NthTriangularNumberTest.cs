namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class NthTriangularNumberTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n * (n + 1) >> 1),
		CreateFolded(5),
		CreateFolded(1),
		CreateFolded(10)
	];
}