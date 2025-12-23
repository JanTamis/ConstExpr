using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPrimeTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 1)
		{
			return false;
		}

		if (n <= 3)
		{
			return true;
		}

		if (n % 2 == 0 || n % 3 == 0)
		{
			return false;
		}

		for (var i = 5; i * i <= n; i += 6)
		{
			if (n % i == 0 || n % (i + 2) == 0)
			{
				return false;
			}
		}

		return true;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (n <= 1)
			{
				return false;
			}

			if (n <= 3)
			{
				return true;
			}

			if (Int32.IsEvenInteger(n) || n % 3 == 0)
			{
				return false;
			}

			for (var i = 5; i * i <= n; i += 6)
			{
				if (n % i == 0 || n % (i + 2) == 0)
				{
					return false;
				}
			}

			return true;
			""", Unknown),
		Create("return true;", 17),
		Create("return true;", 29),
		Create("return false;", 1),
		Create("return false;", 100)
	];
}