using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CelsiusToFahrenheitTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create("return Double.MultiplyAddEstimate(celsius, 1.8, 32D);", Unknown),
		Create("return 32D;", 0.0),
		Create("return 212D;", 100.0),
		Create("return 77D;", 25.0),
	];

	public override string TestMethod => """
		double CelsiusToFahrenheit(double celsius)
		{
			return celsius * 9 / 5 + 32;
		}
		""";
}

