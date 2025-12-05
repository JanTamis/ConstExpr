using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class HypotenuseTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 5D;", 3, 4),
		Create("return 13D;", 5, 12),
		Create("return 10D;", 0, 10),
	];

	public override string TestMethod => """
		double Hypotenuse(int a, int b)
		{
			return Math.Sqrt(a * a + b * b);
		}
		""";
}
