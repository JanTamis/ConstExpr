using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowerTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 32L;", 2, 5),
		Create("return 1L;", 5, 0),
		Create("return 0L;", 2, -3),
		Create("return 1024L;", 2, 10),
	];

	public override string TestMethod => """
		long Power(int baseNum, int exponent)
		{
			if (exponent < 0)
			{
				return 0L;
			}
			if (exponent == 0)
			{
				return 1L;
			}

			var result = 1L;
			var base64 = (long)baseNum;

			while (exponent > 0)
			{
				if (exponent % 2 == 1)
				{
					result *= base64;
				}
				base64 *= base64;
				exponent /= 2;
			}

			return result;
		}
		""";
}
