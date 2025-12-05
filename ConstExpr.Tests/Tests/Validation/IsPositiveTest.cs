using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPositiveTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 42),
		Create("return false;", -10),
		Create("return false;", 0),
	];

	public override string TestMethod => """
		bool IsPositive(int n)
		{
			return n > 0;
		}
		""";
}



