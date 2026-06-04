using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Intersect() optimization - verify that redundant operations and special cases are optimized
/// </summary>
[InheritsTests]
public class LinqIntersectOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Intersect with Empty => Empty (intersection with nothing is nothing)
		var a = x.Intersect([ ]).Count();

		// Empty.Intersect(collection) => Empty (empty intersection anything is empty)
		var b = Enumerable.Empty<int>().Intersect(x).Count();

		// collection.Intersect(collection) => Distinct() (intersection with itself)
		var c = x.Intersect(x).Count();

		// AsEnumerable().Intersect() => Intersect() (skip type cast)
		var d = x.AsEnumerable().Intersect([ 1 ]).Count();

		// ToList().Intersect() => Intersect() (skip materialization)
		var e = x.ToList().Intersect([ 2 ]).Count();

		// ToArray().Intersect() => Intersect() (skip materialization)
		var f = x.ToArray().Intersect([ 3 ]).Count();

		// Distinct().Intersect() => Intersect() (Intersect already applies Distinct)
		var g = x.Distinct().Intersect([ 1, 2 ]).Count();

		// Multiple skip operations
		var h = x.AsEnumerable().ToList().Intersect([ 4 ]).Count();

		// Chained Intersect: Intersect(a).Intersect(b) => Intersect(a.Intersect(b))
		var i = x.Intersect([ 1, 2, 3 ]).Intersect([ 2, 3 ]).Count();

		// Chained Intersect with 3 operations
		var j = x.Intersect([ 1, 2, 3 ]).Intersect([ 2, 3, 4 ]).Intersect([ 3, 4, 5 ]).Count();

		// OrderBy().Intersect().Count() => Intersect().Count() (Count is set-based)
		var k = x.OrderBy(v => v).Intersect([ 1 ]).Count();

		// Reverse().Intersect().Any() => Intersect().Any() (Any is set-based)
		var l = x.Reverse().Intersect([ 5 ]).Any() ? 1 : 0;

		// Intersect on both sides optimized
		var m = x.Distinct().Intersect([ 1, 2 ]).ToList().Count();

		// Regular Intersect (should not be further optimized)
		var n = x.Intersect([ 99 ]).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_AEkyLw(x) * 2 + Count_FQoOgw(x) * 2 + Count_w6J_9Q(x) + Count_VBWycg(x) + Count_utUoqA(x) + Count_w7iHXw(x) + Count_MK3tdQ(x) + (Contains_lhimyw(x) ? 1 : 0) + Count_N_W_CA(x) + Count_0S7iQA(x);"),
		Create(_ => 18, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
		Create(_ => 3, [ new[] { 10, 20, 30 } ]),
		Create(_ => 14, [ new[] { 1, 1, 2, 2, 3 } ]),
	];
}