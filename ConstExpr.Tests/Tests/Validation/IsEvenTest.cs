using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsEvenTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 4),
		Create("return false;", 5),
	];

	public override string TestMethod => """
		bool IsEven(int n)
		{
			if (n < 0) { n = -n; }
			return (n & 1) == 0;
		}
		""";
}

