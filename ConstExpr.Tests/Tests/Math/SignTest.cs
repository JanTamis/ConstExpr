using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SignTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 1;", 100),
		Create("return -1;", -50),
		Create("return 0;", 0),
	];

	public override string TestMethod => """
		int Sign(int n)
		{
			if (n > 0)
			{
				return 1;
			}
			if (n < 0)
			{
				return -1;
			}
			return 0;
		}
		""";
}

