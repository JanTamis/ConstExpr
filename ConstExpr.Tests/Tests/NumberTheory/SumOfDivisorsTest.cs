using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class SumOfDivisorsTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var sum = 0;
		var i = 1;

		while (i <= n)
		{
			if (n % i == 0)
			{
				sum += i;
			}

			i++;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 28;", 12),
		Create("return 1;", 1),
		Create("return 0;", 0)
	];
}