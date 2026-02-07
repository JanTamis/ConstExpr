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
		var k = x.All(v => v > 100) ? 1 : 0;
		
		// Complex: OrderBy().Where().All() => All(combined)
		var l = x.OrderBy(v => v).Where(v => v > 2).All(v => v < 8) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Array.TrueForAll(x, v => v > 0 && v < 10) ? 1 : 0;
			var b = Array.TrueForAll(x, v => v << 1 > 0) ? 1 : 0;
			var c = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var d = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var e = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var f = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var g = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var h = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var i = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var j = Array.TrueForAll(x, v => v > 0) ? 1 : 0;
			var k = Array.TrueForAll(x, v => v > 100) ? 1 : 0;
			var l = Array.TrueForAll(x, v => v > 2 && v < 8) ? 1 : 0;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 10;", new[] { 1, 2, 3, 4, 5 }), // a=1, b-j=1 each (9 total), k=0, l=0 (1,2 fail v>2) = 10
		Create("return 12;", new int[] { }), // All() returns true for empty collection, so all return 1 = 12
		Create("return 9;", new[] { 1, 2, 3, 4, 5, 100 }), // a=0 (100>=10), b-j=1 (9 total), k=0, l=0 (1,2,100 fail) = 9
	];
}
