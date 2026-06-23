using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class FactorialTest() : BaseTest<Func<int, long>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			if (n < 0)
				return -1L;

			if ((uint)n <= 1U)
				return 1L;

			var result = 1L;

			for (var i = 2; i <= n; i++)
				result *= i;

			return result;
		}),
		Create(_ => 120L, [ 5 ]),
		Create(_ => 1L, [ 1 ]),
		Create(_ => -1L, [ -5 ]),
		Create(_ => 3628800L, [ 10 ])
	];
}