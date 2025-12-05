using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ClampTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return 5;", 5, 0, 10),
		Create("return 0;", -5, 0, 10),
		Create("return 10;", 15, 0, 10),
	];

	public override string TestMethod => """
		int Clamp(int value, int min, int max)
		{
			if (value < min)
			{
				return min;
			}

			if (value > max)
			{
				return max;
			}

			return value;
		}
		""";
}
