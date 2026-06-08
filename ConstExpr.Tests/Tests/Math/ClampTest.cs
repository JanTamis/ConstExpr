using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ClampTest() : BaseTest<Func<int, int, int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((value, min, max) =>
	{
		if (value < min)
		{
			return min;
		}

		if (value > max)
		{
			return max;
		}

		return value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((value, min, max) => value < min ? min : value > max ? max : value),
		Create((_, _, _) => 5, [ 5, 0, 10 ]),
		Create((_, _, _) => 0, [ -5, 0, 10 ]),
		Create((_, _, _) => 10, [ 15, 0, 10 ])
	];
}