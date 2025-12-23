using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CelsiusToFahrenheitTest() : BaseTest<Func<double, double>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(celsius => celsius * 9 / 5 + 32);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return Double.MultiplyAddEstimate(celsius, 1.8, 32D);", Unknown),
		Create("return 32D;", 0.0),
		Create("return 212D;", 100.0),
		Create("return 77D;", 25.0)
	];
}