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
			var d = x.Distinct().Count(c => c != 1);
			var e = x.Distinct().Count(c => c != 2);
			var f = x.Distinct().Count(c => c != 3);
			var g = x.Distinct().Count(c => (uint)c - 1 > 1); // Count of not 1 and not 2
			var h = x.Distinct().Count(c => c != 4);
			var i = x.Distinct().Count(c => (uint)c - 1 > 1); // Count of not 1 and not 2
			var j = x.Distinct().Count(c => (uint)c - 1 > 2); // Count of not 1 and not 2 and not 3S
			var k = x.Distinct().Count(c => c != 1);
			var l = x.Any(a => a != 5) ? 1 : 0;
			var m = x.Distinct().Count(c => (uint)c - 1 > 1);
			var n = x.Distinct().Count(c => c != 99);
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", Unknown),
		Create("return 38;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 42;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }), 
		Create("return 20;", new[] { 10, 20, 30 }),
		Create("return 34;", new[] { 10, 20, 30 }),
		Create("return 28;", new[] { 1, 1, 2, 2, 3 }),
		Create("return 21;", new[] { 1, 1, 2, 2, 3 }),
	];
}

