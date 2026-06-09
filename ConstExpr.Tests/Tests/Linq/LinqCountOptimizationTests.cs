using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Count() optimization - verify that unnecessary operations before Count() are removed
/// </summary>
[InheritsTests]
public class LinqCountOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Count() => Count(predicate)
		var a = x.Where(v => v > 3).Count();

		// OrderBy(...).Count() => Count()
		var b = x.OrderBy(v => v).Count();

		// OrderByDescending(...).Count() => Count()
		var c = x.OrderByDescending(v => v).Count();

		// Reverse().Count() => Count()
		var d = x.Reverse().Count();

		// AsEnumerable().Count() => Count()
		var e = x.AsEnumerable().Count();

		// OrderBy().ThenBy().Count() => Count()
		var f = x.OrderBy(v => v).ThenBy(v => v * 2).Count();

		// OrderBy().Where().Count() => Count(predicate)
		var g = x.OrderBy(v => v).Where(v => v > 2).Count();

		// Complex: OrderBy().ThenBy().Reverse().Where().Count() => Count(predicate)
		var h = x.OrderBy(v => v).ThenBy(v => v * 2).Reverse().Where(v => v < 5).Count();

		// Distinct should NOT be optimized (reduces count!)
		var i = x.Distinct().Concat(x).Concat(x).Count();

		// Select should be optimized away
		var j = x.Select(v => v * 2).Count();

		// Multiple chained Where statements should be combined
		var k = x.Where(v => v > 2).Where(v => v < 10).Count();

		// Three chained Where statements
		var l = x.Where(v => v > 1).Where(v => v < 8).Where(v => v % 2 == 0).Count();

		// Where with operations that don't affect count
		var m = x.OrderBy(v => v).Where(v => v > 2).Where(v => v < 10).Count();

		// Complex chain with multiple Where statements
		var n = x.Where(v => v > 1).OrderBy(v => v).Where(v => v < 8).Reverse().Where(v => v % 2 == 0).Count();

		var o = x.GroupBy(v => v % 3).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Length * 8 + Count_ltHwqA(x) * 2 + Count_c712xg(x) * 2 + Count_vwaqjw(x) + Count_BV75KA(x) + Count__2hA9w(x) + Count_w6J_9Q(x) + Count_cdonHg(x);"),
		Create(_ => 67, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
	];
}