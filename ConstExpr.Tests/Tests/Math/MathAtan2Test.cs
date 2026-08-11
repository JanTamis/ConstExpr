namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathAtan2Test : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((y, x) => System.Math.Atan2(y, x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan2(y, x);"),
	];
}