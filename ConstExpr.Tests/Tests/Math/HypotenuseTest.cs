namespace ConstExpr.Tests.Math;

[InheritsTests]
public class HypotenuseTest : BaseTestWithRandomValues<Func<int, int, double>>
{
	public override string TestMethod => GetString((a, b) => System.Math.Sqrt(a * a + b * b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.Sqrt(a * a + b * b)),
	];
}