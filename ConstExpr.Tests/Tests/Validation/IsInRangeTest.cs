using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return false;", 15, 1, 10),
		Create("return true;", 1, 1, 10),
	];

	public override string TestMethod => """
		bool IsInRange(int value, int min, int max)
		{
			return value >= min && value <= max;
		}
		""";
}


