using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class NthTriangularNumberTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 15;", 5),
		Create("return 1;", 1),
		Create("return 55;", 10),
	];

	public override string TestMethod => """
		int NthTriangularNumber(int n)
		{
			return n * (n + 1) / 2;
		}
		""";
}

