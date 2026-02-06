namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Except() optimization - verify that redundant operations and special cases are optimized
/// </summary>
[InheritsTests]
public class LinqExceptOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Except with Empty => Distinct() (removing nothing)
		var a = x.Except([ ]).Count();

		// Empty.Except(collection) => Empty (empty minus anything is empty)
		var b = Enumerable.Empty<int>().Except(x).Count();

		// collection.Except(collection) => Empty (set minus itself)
		var c = x.Except(x).Count();

		// AsEnumerable().Except() => Except() (skip type cast)
		var d = x.AsEnumerable().Except([ 1 ]).Count();

		// ToList().Except() => Except() (skip materialization)
		var e = x.ToList().Except([ 2 ]).Count();

		// ToArray().Except() => Except() (skip materialization)
		var f = x.ToArray().Except([ 3 ]).Count();

		// Distinct().Except() => Except() (Except already applies Distinct)
		var g = x.Distinct().Except([ 1, 2 ]).Count();

		// Multiple skip operations
		var h = x.AsEnumerable().ToList().Except([ 4 ]).Count();

		// Chained Except: Except(a).Except(b) => Except(a.Concat(b))
		var i = x.Except([ 1 ]).Except([ 2 ]).Count();

		// Chained Except with 3 operations
		var j = x.Except([ 1 ]).Except([ 2 ]).Except([ 3 ]).Count();

		// OrderBy().Except().Count() => Except().Count() (Count is set-based)
		var k = x.OrderBy(v => v).Except([ 1 ]).Count();

		// Reverse().Except().Any() => Except().Any() (Any is set-based)
		var l = x.Reverse().Except([ 5 ]).Any() ? 1 : 0;

		// Except on both sides optimized
		var m = x.Distinct().Except(new[] { 1, 2 }.ToList()).Count();

		// Regular Except (should not be further optimized)
		var n = x.Except([ 99 ]).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Distinct().Count();
			var b = 0;
			var c = 0;
			var d = x.Except([1]).Count();
			var e = x.Except([2]).Count();
			var f = x.Except([3]).Count();
			var g = x.Except([1, 2]).Count();
			var h = x.Except([4]).Count();
			var i = x.Except([1].Concat([2])).Count();
			var j = x.Except([1].Concat([2]).Concat([3])).Count();
			var k = x.Except([1]).Count();
			var l = x.Except([5]).Any() ? 1 : 0;
			var m = x.Except([1, 2]).Count();
			var n = x.Except([99]).Count();
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", Unknown),
		Create("return 38;", new[] { 1, 2, 3, 4, 5 }), 
		// a = 5 (distinct count)
		// b = 0 (empty)
		// c = 0 (self-except)
		// d = 4 (2,3,4,5)
		// e = 4 (1,3,4,5)
		// f = 4 (1,2,4,5)
		// g = 3 (3,4,5)
		// h = 4 (1,2,3,5)
		// i = 3 (3,4,5)
		// j = 2 (4,5)
		// k = 4 (2,3,4,5)
		// l = 1 (has elements 1,2,3,4)
		// m = 3 (3,4,5)
		// n = 5 (all elements, 99 not in array)
		// Total = 5+0+0+4+4+4+3+4+3+2+4+1+3+5 = 42
		Create("return 42;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }), 
		Create("return 20;", new[] { 10, 20, 30 }),
		// a = 3 (distinct count)
		// b = 0 (empty)
		// c = 0 (self-except)
		// d = 3 (all, 1 not in array)
		// e = 3 (all, 2 not in array)
		// f = 3 (all, 3 not in array)
		// g = 3 (all)
		// h = 3 (all, 4 not in array)
		// i = 3 (all)
		// j = 3 (all)
		// k = 3 (all, 1 not in array)
		// l = 1 (has elements)
		// m = 3 (all)
		// n = 3 (all, 99 not in array)
		// Total = 3+0+0+3+3+3+3+3+3+3+3+1+3+3 = 34
		Create("return 34;", new[] { 10, 20, 30 }),
		Create("return 28;", new[] { 1, 1, 2, 2, 3 }), 
		// a = 3 (distinct: 1,2,3)
		// b = 0 (empty)
		// c = 0 (self-except)
		// d = 2 (2,3)
		// e = 2 (1,3)
		// f = 2 (1,2)
		// g = 1 (3)
		// h = 3 (all, 4 not in array)
		// i = 1 (3)
		// j = 0 (nothing left)
		// k = 2 (2,3)
		// l = 1 (has elements 1,2,3)
		// m = 1 (3)
		// n = 3 (all distinct, 99 not in array)
		// Total = 3+0+0+2+2+2+1+3+1+0+2+1+1+3 = 21
		Create("return 21;", new[] { 1, 1, 2, 2, 3 }),
	];
}

