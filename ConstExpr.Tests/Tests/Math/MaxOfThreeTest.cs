using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest() : BaseTest<Func<int, int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b, c) =>
	{
		var max = a;

		if (b > max)
		{
			max = b;
		}

		if (c > max)
		{
			max = c;
		}

		return max;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b, c) =>
		{
			var max = a;

			if (b > max)
			{
				max = b;
			}

			if (c > max)
			{
				max = c;
			}

			return max;
		}),
		Create((_, _, _) => 10, [ 5, 10, 3 ]),
		Create((_, _, _) => 5, [ 5, 5, 5 ])
	];
}