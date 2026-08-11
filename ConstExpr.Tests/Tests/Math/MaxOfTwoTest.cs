namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfTwoTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) => a > b ? a : b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Int32.Max(a, b)),
	];
}