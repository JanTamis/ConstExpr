namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AbsoluteDifferenceTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var diff = a - b;

		return diff < 0 ? -diff : diff;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAbs(a - b);"),
	];
}