using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest() : BaseTest<Func<double, double, double>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((value, percentage) => value * percentage / 100);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return value * percentage * 0.01;", Unknown, Unknown),
		Create("return 25D;", 100.0, 25.0),
		Create("return 0D;", 50.0, 0.0),
		Create("return 7.5D;", 50.0, 15.0)
	];
}