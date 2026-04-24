using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CelsiusToFahrenheitTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(celsius => celsius * 9 / 5 + 32);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.MultiplyAddEstimate(celsius, 1.8, 32D);"),
		Create("return 32D;", 0.0),
		Create("return 212D;", 100.0),
		Create("return 77D;", 25.0)
	];
}