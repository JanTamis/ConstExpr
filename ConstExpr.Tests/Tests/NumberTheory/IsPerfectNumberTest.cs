using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPerfectNumberTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 6),
		Create("return true;", 28),
		Create("return false;", 12),
		Create("return false;", 1),
	];

	public override string TestMethod => """
		bool IsPerfectNumber(int n)
		{
			if (n <= 1)
			{
				return false;
			}
			
			var sum = 1;
			var i = 2;
			
			while (i * i <= n)
			{
				if (n % i == 0)
				{
					sum += i;

					if (i * i != n)
					{
						sum += n / i;
					}
				}

				i++;
			}
			
			return sum == n;
		}
		""";
}

