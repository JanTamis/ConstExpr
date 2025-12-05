using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class CountPrimeFactorsTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 3;", 12),
		Create("return 0;", 1),
	];

	public override string TestMethod => """
		int CountPrimeFactors(int n)
		{
			var count = 0;
			var num = Math.Abs(n);
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
		}
		""";
}

