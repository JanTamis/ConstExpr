using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class FactorialTest() : BaseTest<Func<int, long>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
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
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (n < 0)
			{
				return -1;
			}

			if ((uint)n <= 1U)
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
		Create("return 3628800L;", 10)
	];
}