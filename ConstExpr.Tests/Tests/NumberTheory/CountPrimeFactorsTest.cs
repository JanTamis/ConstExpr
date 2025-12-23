using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class CountPrimeFactorsTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		var count = 0;
		var num = System.Math.Abs(n);
		var i = 2;

		while (i * i <= num)
		{
			while (num % i == 0)
			{
				count++;
				num /= i;
			}

			i++;
		}

		if (num > 1)
		{
			count++;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var count = 0;
			var num = Int32.Abs(n);
			var i = 2;
			
			while (i * i <= num)
			{
				while (num % i == 0)
				{
					count++;
					num /= i;
				}
			
				i++;
			}
			
			if (num > 1)
			{
				count++;
			}
			
			return count;
			""", Unknown),
		Create("return 3;", 12),
		Create("return 0;", 1)
	];
}