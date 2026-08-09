namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n * (n + 1) >> 1),
		CreateFolded(10),
		CreateFolded(0),
		CreateFolded(100)
	];
}