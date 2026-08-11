namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathRoundTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Round(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Round(x)),
	];
}