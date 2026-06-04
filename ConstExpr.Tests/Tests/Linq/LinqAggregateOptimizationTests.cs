using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Aggregate() optimization - verify that unnecessary operations before Aggregate() are removed
/// and Aggregate patterns are optimized to Sum when appropriate
/// </summary>
[InheritsTests]
public class LinqAggregateOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Aggregate(...) => Aggregate(...)
		var a = x.AsEnumerable().Aggregate((acc, v) => acc + v);

		// ToList().Aggregate(...) => Aggregate(...)
		var b = x.ToList().Aggregate((acc, v) => acc + v);

		// ToArray().Aggregate(...) => Aggregate(...)
		var c = x.ToArray().Aggregate((acc, v) => acc + v);

		// Multiple chained: ToList().AsEnumerable().Aggregate(...) => Aggregate(...)
		var d = x.ToList().AsEnumerable().Aggregate((acc, v) => acc + v);

		// With seed: ToArray().Aggregate(seed, func) => Aggregate(seed, func)
		var e = x.ToArray().Aggregate(0, (acc, v) => acc + v);

		// With seed and result selector: AsEnumerable().Aggregate(seed, func, selector) => Aggregate(seed, func, selector)
		var f = x.AsEnumerable().Aggregate(0, (acc, v) => acc + v, acc => acc * 2);

		// Aggregate to Sum: Aggregate((acc, v) => acc + v) => Sum()
		var g1 = x.Aggregate((acc, v) => acc + v);

		// Aggregate to Sum: Aggregate((acc, v) => v + acc) => Sum() (reverse order)
		var g2 = x.Aggregate((acc, v) => v + acc);

		// Aggregate to Sum with seed 0: Aggregate(0, (acc, v) => acc + v) => Sum()
		var h = x.Aggregate(0, (acc, v) => acc + v);

		// OrderBy should NOT be optimized (changes the order of aggregation!)
		var i = x.OrderBy(v => v).Aggregate((acc, v) => acc + v);

		// Select should NOT be optimized (changes the elements being aggregated!)
		var j = x.Select(v => v * 2).Aggregate((acc, v) => acc + v);

		// Where should NOT be optimized (filters elements!)
		var k = x.Where(v => v > 2).Aggregate((acc, v) => acc + v);

		// Distinct should NOT be optimized (removes duplicates!)
		var l = x.Distinct().Aggregate((acc, v) => acc + v);

		// Non-addition aggregate should NOT be optimized to Sum
		var m = x.Aggregate((acc, v) => acc * v);

		// Non-zero seed should be optimized to Sum
		var n = x.Aggregate(10, (acc, v) => acc + v);

		return a + b + c + d + e + f + g1 + g2 + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return TensorPrimitives.Sum(x) * 12 + Sum_dcMRsA(x) + Sum_GrzDbA(x) + Sum_CY7UDw(x) + Aggregate_sv6FOA(x) + 10;"),
		Create("return 367;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 1063;", new[] { 1, 2, 3, 4, 5, 6 }),
	];
}