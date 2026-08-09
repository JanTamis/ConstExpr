namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CelsiusToFahrenheitTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(celsius => celsius * 9 / 5 + 32);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(celsius => Double.MultiplyAddEstimate(celsius, 1.8, 32D)),
		CreateFolded(0.0),
		CreateFolded(100.0),
		CreateFolded(25.0)
	];
}