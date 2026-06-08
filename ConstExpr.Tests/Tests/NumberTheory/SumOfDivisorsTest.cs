using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class SumOfDivisorsTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 28, [ 12 ]),
		Create(_ => 1, [ 1 ]),
		Create(_ => 0, [ 0 ])
	];
}