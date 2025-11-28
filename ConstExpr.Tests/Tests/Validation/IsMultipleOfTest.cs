using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsMultipleOfTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", 15, 5),
		Create("return false;", 17, 3),
		Create("return true;", 0, 5),
	];

	public override string TestMethod => """
		bool IsMultipleOf(int n, int divisor)
		{
			return divisor != 0 && n % divisor == 0;
		}
		""";
}

