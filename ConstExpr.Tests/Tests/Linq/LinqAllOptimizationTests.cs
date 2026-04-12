namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for All() optimization - verify that unnecessary operations before All() are removed
/// and Where predicates are combined with All predicates
/// </summary>
[InheritsTests]
public class LinqAllOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).All() => All(combined predicates)
		var a = x.Where(v => v > 0).All(v => v < 10) ? 1 : 0;

		// Select(...).All() => All()
		var b = x.Select(v => v * 2).All(v => v > 0) ? 1 : 0;

		// Distinct().All() => All()
		var c = x.Distinct().All(v => v > 0) ? 1 : 0;

		// OrderBy(...).All() => All()
		var d = x.OrderBy(v => v).All(v => v > 0) ? 1 : 0;

		// OrderByDescending(...).All() => All()
		var e = x.OrderByDescending(v => v).All(v => v > 0) ? 1 : 0;

		// Reverse().All() => All()
		var f = x.Reverse().All(v => v > 0) ? 1 : 0;

		// AsEnumerable().All() => All()
		var g = x.AsEnumerable().All(v => v > 0) ? 1 : 0;

		// ToList().All() => All()
		var h = x.ToList().All(v => v > 0) ? 1 : 0;

		// ToArray().All() => All()
		var i = x.ToArray().All(v => v > 0) ? 1 : 0;

		// All elements satisfy condition
		var j = x.All(v => v > 0) ? 1 : 0;

		// No elements satisfy condition
		var k = x.Concat(x).All(v => v > 100) ? 1 : 0;
		
		// Complex: OrderBy().Where().All() => All(combined)
		var l = x.OrderBy(v => v).Where(v => v > 2).All(v => v < 8) ? 1 : 0;

		var m = x.Append(5).All(v => v > 3) ? 1 : 0;

		var n = x.Prepend(5).All(v => v > 3) ? 1 : 0;

		var o = x.DefaultIfEmpty().All(v => v > 3) ? 1 : 0;

		var p = x.DefaultIfEmpty(5).All(v => v > 3) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o + p;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Array.TrueForAll(x, v => (uint)v <= 10U) ? 1 : 0;
			var b = Array.TrueForAll(x, v => v << 1 > 0) ? 1 : 0;
			var c = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var d = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var e = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var f = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var g = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var h = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var i = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var j = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var k = Array.TrueForAll(x, v => v > 100) && Array.TrueForAll(x, v => v > 100) ? 1 : 0;
			var l = Array.TrueForAll(x, v => (uint)(v - 2) <= 6U) ? 1 : 0;
			var m = Array.TrueForAll(x, v => v > 3) ? 1 : 0;
			var n = Array.TrueForAll(x, v => v > 3) ? 1 : 0;
			var p = Array.TrueForAll(x, v => v > 3) ? 1 : 0;
			""", Unknown),
		Create("return 11;", new[] { 1, 2, 3, 4, 5 }), // a=1, b-j=1 each (10), k=0, l=1 (Where(v>2)→{3,4,5} All(v<8)→true), m=0, n=0, p=0 = 11
		Create("return 12;", new int[] { }), // All() returns true for empty collection, so all return 1 = 12
		Create("return 9;", new[] { 1, 2, 3, 4, 5, 100 }), // a=0 (100>=10), b-j=1 (9 total), k=0, l=0 (100≥8), m=0, n=0, p=0 = 9
	];
}
