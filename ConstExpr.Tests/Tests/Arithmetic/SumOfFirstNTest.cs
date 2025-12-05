using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 55;", 10),
		Create("return 0;", 0),
		Create("return 5050;", 100),
	];

	public override string TestMethod => """
		int SumOfFirstN(int n)
		{
			return n * (n + 1) / 2;
		}
		""";
}

