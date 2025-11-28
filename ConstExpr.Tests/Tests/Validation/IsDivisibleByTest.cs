using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsDivisibleByTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", 10, 5),
		Create("return false;", 10, 3),
		Create("return false;", 0, 0),
	];

	public override string TestMethod => """
		bool IsDivisibleBy(int n, int divisor)
		{
			return divisor != 0 && n % divisor == 0;
		}
		""";
}


