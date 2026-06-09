using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPerfectNumberTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
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
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => true, [ 6 ]),
		Create(_ => true, [ 28 ]),
		Create(_ => false, [ 12 ]),
		Create(_ => false, [ 1 ])
	];
}