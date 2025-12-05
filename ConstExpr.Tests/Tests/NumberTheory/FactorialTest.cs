using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class FactorialTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		if (n < 0)
		{
			return -1;
		}
		
		if (n is 1 or 2)
		{
			return 1;
		}
		
		var result = 1L;
		
		for (var i = 2; i <= n; i++)
		{
			result *= i;
		}
		
		return result;
		""", Unknown),
		Create("return 120L;", 5),
		Create("return 1L;", 1),
		Create("return -1L;", -5),
		Create("return 3628800L;", 10),
	];

	public override string TestMethod => """
		long Factorial(int n)
		{
			if (n < 0)
			{
				return -1;
			}

			if (n == 0 || n == 1)
			{
				return 1;
			}

			var result = 1L;

			for (var i = 2; i <= n; i++)
			{
				result *= i;
			}

			return result;
		}
		""";
}
