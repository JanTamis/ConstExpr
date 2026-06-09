using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class CountOccurrencesTest() : BaseTest<Func<int, int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((target, numbers) =>
	{
		var count = 0;

		foreach (var num in numbers)
		{
			if (num == target)
			{
				count++;
			}
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => 4, [ 5, new[] { 5, 5, 10, 5, 20, 5 } ]),
		Create((_, _) => 0, [ 100, new[] { 1, 2, 3, 4, 5 } ]),
		Create((_, _) => 2, [ 7, new[] { 7, 14, 21, 7 } ])
	];
}