using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AverageTest() : BaseTest<Func<int[], double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length == 0)
		{
			return 0.0;
		}

		var sum = 0;

		foreach (var num in numbers)
		{
			sum += num;
		}

		return (double) sum / numbers.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 30D, [ new[] { 10, 20, 30, 40, 50 } ]),
		Create(_ => 15D, [ new[] { 5, 15, 25 } ]),
		Create(_ => 0D, [ System.Array.Empty<int>() ])
	];
}