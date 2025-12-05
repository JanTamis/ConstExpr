using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return value * percentage * 0.01;", Unknown, Unknown),
		Create("return 25D;", 100.0, 25.0),
		Create("return 0D;", 50.0, 0.0),
		Create("return 7.5D;", 50.0, 15.0),
	];

	public override string TestMethod => """
		double Percentage(double value, double percentage)
		{
			return value * percentage / 100;
		}
		""";
}

