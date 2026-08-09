namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AbsoluteDifferenceTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var diff = a - b;

		return diff < 0 ? -diff : diff;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAbs(a - b);"),
		CreateFolded(10, 5),
		CreateFolded(-10, 20),
		CreateFolded(42, 42)
	];
}