using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
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

