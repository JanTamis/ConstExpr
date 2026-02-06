namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Intersect() optimization - verify that redundant operations and special cases are optimized
/// </summary>
[InheritsTests]
public class LinqIntersectOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Intersect with Empty => Empty (intersection with nothing is nothing)
		var a = x.Intersect([]).Count();

		// Empty.Intersect(collection) => Empty (empty intersection anything is empty)
		var b = Enumerable.Empty<int>().Intersect(x).Count();

		// collection.Intersect(collection) => Distinct() (intersection with itself)
		var c = x.Intersect(x).Count();

		// AsEnumerable().Intersect() => Intersect() (skip type cast)
		var d = x.AsEnumerable().Intersect([1]).Count();

		// ToList().Intersect() => Intersect() (skip materialization)
		var e = x.ToList().Intersect([2]).Count();

		// ToArray().Intersect() => Intersect() (skip materialization)
		var f = x.ToArray().Intersect([3]).Count();

		// Distinct().Intersect() => Intersect() (Intersect already applies Distinct)
		var g = x.Distinct().Intersect([1, 2]).Count();

		// Multiple skip operations
		var h = x.AsEnumerable().ToList().Intersect([4]).Count();

		// Chained Intersect: Intersect(a).Intersect(b) => Intersect(a.Intersect(b))
		var i = x.Intersect([1, 2, 3]).Intersect([2, 3]).Count();

		// Chained Intersect with 3 operations
		var j = x.Intersect([1, 2, 3]).Intersect([2, 3, 4]).Intersect([3, 4, 5]).Count();

		// OrderBy().Intersect().Count() => Intersect().Count() (Count is set-based)
		var k = x.OrderBy(v => v).Intersect([1]).Count();

		// Reverse().Intersect().Any() => Intersect().Any() (Any is set-based)
		var l = x.Reverse().Intersect([5]).Any() ? 1 : 0;

		// Intersect on both sides optimized
		var m = x.Distinct().Intersect(new[] { 1, 2 }.ToList()).Count();

		// Regular Intersect (should not be further optimized)
		var n = x.Intersect([99]).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = 0;
			var b = 0;
			var c = x.Distinct().Count();
			var d = x.Intersect([1]).Count();
			var e = x.Intersect([2]).Count();
			var f = x.Intersect([3]).Count();
			var g = x.Intersect([1, 2]).Count();
			var h = x.Intersect([4]).Count();
			var i = x.Intersect([1, 2, 3].Intersect([2, 3])).Count();
			var j = x.Intersect([1, 2, 3].Intersect([2, 3, 4]).Intersect([3, 4, 5])).Count();
			var k = x.Intersect([1]).Count();
			var l = x.Intersect([5]).Any() ? 1 : 0;
			var m = x.Intersect([1, 2]).Count();
			var n = x.Intersect([99]).Count();
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", Unknown),
		// Test case: [1, 2, 3, 4, 5]
		// a = 0 (empty intersect)
		// b = 0 (empty intersect)
		// c = 5 (distinct count: 1,2,3,4,5)
		// d = 1 (only 1)
		// e = 1 (only 2)
		// f = 1 (only 3)
		// g = 2 (1,2)
		// h = 1 (only 4)
		// i = 2 ([1,2,3].Intersect([2,3]) = [2,3], x.Intersect([2,3]) = [2,3])
		// j = 1 ([1,2,3].Intersect([2,3,4]) = [2,3], [2,3].Intersect([3,4,5]) = [3], x.Intersect([3]) = [3])
		// k = 1 (only 1)
		// l = 1 (5 in x)
		// m = 2 (1,2)
		// n = 0 (99 not in array)
		// Total = 0+0+5+1+1+1+2+1+2+1+1+1+2+0 = 18
		Create("return 18;", new[] { 1, 2, 3, 4, 5 }),
		// Test case: []
		// a = 0 (empty intersect)
		// b = 0 (empty intersect)
		// c = 0 (empty distinct)
		// d = 0 (no intersection)
		// e = 0 (no intersection)
		// f = 0 (no intersection)
		// g = 0 (no intersection)
		// h = 0 (no intersection)
		// i = 0 (no intersection)
		// j = 0 (no intersection)
		// k = 0 (no intersection)
		// l = 0 (no elements)
		// m = 0 (no intersection)
		// n = 0 (no intersection)
		// Total = 0+0+0+0+0+0+0+0+0+0+0+0+0+0 = 0
		Create("return 0;", new int[] { }),
		// Test case: [10, 20, 30]
		// a = 0 (empty intersect)
		// b = 0 (empty intersect)
		// c = 3 (distinct: 10,20,30)
		// d = 0 (1 not in array)
		// e = 0 (2 not in array)
		// f = 0 (3 not in array)
		// g = 0 (1,2 not in array)
		// h = 0 (4 not in array)
		// i = 0 (no common elements)
		// j = 0 (no common elements)
		// k = 0 (1 not in array)
		// l = 0 (5 not in array)
		// m = 0 (1,2 not in array)
		// n = 0 (99 not in array)
		// Total = 0+0+3+0+0+0+0+0+0+0+0+0+0+0 = 3
		Create("return 3;", new[] { 10, 20, 30 }),
		// Test case: [1, 1, 2, 2, 3]
		// a = 0 (empty intersect)
		// b = 0 (empty intersect)
		// c = 3 (distinct: 1,2,3)
		// d = 1 (1)
		// e = 1 (2)
		// f = 1 (3)
		// g = 2 (1,2)
		// h = 0 (4 not in array)
		// i = 2 ([1,2,3].Intersect([2,3]) = [2,3], x.Intersect([2,3]) = [2,3])
		// j = 1 ([1,2,3].Intersect([2,3,4]) = [2,3], [2,3].Intersect([3,4,5]) = [3], x.Intersect([3]) = [3])
		// k = 1 (1)
		// l = 0 (5 not in array)
		// m = 2 (1,2)
		// n = 0 (99 not in array)
		// Total = 0+0+3+1+1+1+2+0+2+1+1+0+2+0 = 14
		Create("return 14;", new[] { 1, 1, 2, 2, 3 }),
	];
}

