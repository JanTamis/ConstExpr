namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CubeTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n => n * n * n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}