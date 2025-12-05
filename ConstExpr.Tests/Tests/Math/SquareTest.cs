using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SquareTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 25;", 5),
		Create("return 0;", 0),
		Create("return 100;", -10),
	];

	public override string TestMethod => """
		int Square(int n)
		{
			return n * n;
		}
		""";
}

