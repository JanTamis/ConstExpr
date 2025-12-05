using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ModuloTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 3;", 13, 10),
		Create("return 2;", -8, 5),
		Create("return 0;", 10, 0),
	];

	public override string TestMethod => """
		int Modulo(int dividend, int divisor)
		{
			if (divisor == 0)
			{
				return 0;
			}
			
			var result = dividend % divisor;
			if (result < 0)
			{
				result += divisor;
			}
			return result;
		}
		""";
}

