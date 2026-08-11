namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ScaleBTest : BaseTestWithRandomValues<Func<double, int, double>>
{
	public override string TestMethod => GetString((x, n) => System.Math.ScaleB(x, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastScaleB(x, n);")
	];
}