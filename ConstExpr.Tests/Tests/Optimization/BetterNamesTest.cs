namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class BetterNamesTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		var s1 = x * x + y * y;
		var s2 = x * x + y * y;
		return s1 + s2;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => x * x + y * y << 1),
		CreateFolded(3, 4)
	];
}