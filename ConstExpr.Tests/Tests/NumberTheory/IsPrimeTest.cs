using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPrimeTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if ((uint)(n - 2) > 1u || Int32.IsEvenInteger(n) || n % 3 == 0)
				return false;

			for (var i = 5; i * i <= n; i += 6)
			{
				if (n % i == 0 || n % (i + 2) == 0)
					return false;
			}

			return true;
			"""),
		Create(_ => true, [ 17 ]),
		Create(_ => true, [ 29 ]),
		Create(_ => false, [ 1 ]),
		Create(_ => false, [ 100 ])
	];
}