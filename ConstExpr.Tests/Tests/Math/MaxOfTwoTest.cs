using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfTwoTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return Int32.Max(a, b);", Unknown, Unknown),
		Create("return 10;", 5, 10),
		Create("return 20;", -10, 20),
		Create("return 0;", 0, 0),
	];

	public override string TestMethod => """
		int MaxOfTwo(int a, int b)
		{
			return a > b ? a : b;
		}
		""";
}

