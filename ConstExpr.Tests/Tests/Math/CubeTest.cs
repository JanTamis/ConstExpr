using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CubeTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 125;", 5),
		Create("return 0;", 0),
		Create("return -8;", -2),
	];

	public override string TestMethod => """
		int Cube(int n)
		{
			return n * n * n;
		}
		""";
}

