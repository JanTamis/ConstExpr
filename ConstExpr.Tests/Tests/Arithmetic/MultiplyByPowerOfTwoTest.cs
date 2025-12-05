using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class MultiplyByPowerOfTwoTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 40;", 10, 2),
		Create("return 0;", 0, 5),
		Create("return 128;", 4, 5),
	];

	public override string TestMethod => """
		int MultiplyByPowerOfTwo(int n, int power)
		{
			return n << power;
		}
		""";
}

