namespace ConstExpr.Tests.Linq;

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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var d = Count_wX25Rw(x);
			var e = Count_IyhE7Q(x);
			var f = Count_lIg1kw(x);
			var g = Count_vFVZUg(x);
			var h = Count_4oc4tg(x);
			var i = Count_vFVZUg(x);
			var j = Count_87tGZw(x);
			var k = Count_wX25Rw(x);
			var l = Array.Exists(x, x => x != 5) ? 1 : 0;
			var m = Count_vFVZUg(x);
			var n = Count_uLsNyg(x);
			
			return d + e + f + g + h + i + j + k + l + m + n;
			"""),
		Create("return 42;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
		Create("return 34;", new[] { 10, 20, 30 }),
		Create("return 21;", new[] { 1, 1, 2, 2, 3 }),
	];
}