namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToLookup() optimization - verify redundant materialization removal, ordering removal,
/// identity element-selector removal, Select folding, and Where merging.
/// Note: Unlike ToDictionary, Distinct is NOT stripped because ToLookup groups duplicates
/// and removing Distinct would change the group sizes.
/// </summary>
[InheritsTests]
public class LinqToLookupOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().ToLookup() => ToLookup()
		var a = x.AsEnumerable().ToLookup(v => v).Count;

		// ToList().ToLookup() => ToLookup()
		var b = x.ToList().ToLookup(v => v).Count;

		// ToArray().ToLookup() => ToLookup()
		var c = x.ToArray().ToLookup(v => v).Count;

		// ToList().ToArray().ToLookup() => ToLookup()
		var d = x.ToList().ToArray().ToLookup(v => v).Count;

		// AsEnumerable().ToLookup(keySelector, elementSelector) => ToLookup(keySelector, elementSelector)
		var e = x.AsEnumerable().ToLookup(v => v, v => v * 2).Count;

		// ToList().ToLookup(keySelector, elementSelector) => ToLookup(keySelector, elementSelector)
		var f = x.ToList().ToLookup(v => v, v => v * 2).Count;

		// OrderBy().ToLookup() => ToLookup() (ordering doesn't affect lookup)
		var g = x.OrderBy(v => v).ToLookup(v => v).Count;

		// OrderByDescending().ToLookup() => ToLookup()
		var h = x.OrderByDescending(v => v).ToLookup(v => v).Count;

		// Reverse().ToLookup() => ToLookup()
		var i = x.Reverse().ToLookup(v => v).Count;

		// Select(x => x).ToLookup() => ToLookup() (identity Select is a no-op)
		var j = x.Select(v => v).ToLookup(v => v).Count;

		// ToLookup(keySelector, x => x) => ToLookup(keySelector) (identity element-selector)
		var k = x.ToLookup(v => v, v => v).Count;

		// Select(selector).ToLookup(keySelector) => ToLookup(composedKey, selector)
		var l = x.Select(v => v * 10).ToLookup(v => v).Count;

		// Where(predicate).ToLookup(keySelector) => Where(predicate).ToLookup(keySelector)
		var m = x.Where(v => v > 0).ToLookup(v => v).Count;

		// Where(p1).Where(p2).ToLookup(keySelector) => Where(p1 && p2).ToLookup(keySelector) (merge chained Where)
		var n = x.Where(v => v > 0).Where(v => v < 10).ToLookup(v => v).Count;

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.ToLookup(v => v).Count;
			var b = x.ToLookup(v => v).Count;
			var c = x.ToLookup(v => v).Count;
			var d = x.ToLookup(v => v).Count;
			var e = x.ToLookup(v => v, v => v << 1).Count;
			var f = x.ToLookup(v => v, v => v << 1).Count;
			var g = x.ToLookup(v => v).Count;
			var h = x.ToLookup(v => v).Count;
			var i = x.ToLookup(v => v).Count;
			var j = x.ToLookup(v => v).Count;
			var k = x.ToLookup(v => v).Count;
			var l = x.ToLookup(v => v * 10, v => v * 10).Count;
			var m = x.Where(v => v > 0).ToLookup(v => v).Count;
			var n = x.Where(v => (uint)v < 10U).ToLookup(v => v).Count;

			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", Unknown),
		Create("""
			var a = new Lookup_4jhdag().Count;
			var b = new Lookup_4jhdag().Count;
			var c = new Lookup_4jhdag().Count;
			var d = new Lookup_4jhdag().Count;
			var e = new Lookup_sMxTiw().Count;
			var f = new Lookup_sMxTiw().Count;
			var g = new Lookup_4jhdag().Count;
			var h = new Lookup_4jhdag().Count;
			var i = new Lookup_4jhdag().Count;
			var j = new Lookup_4jhdag().Count;
			var k = new Lookup_4jhdag().Count;
			var l = new Lookup_slRS_g().Count;
			var m = new Lookup_4jhdag().Count;
			var n = new Lookup_4jhdag().Count;

			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", new[] { 1, 2, 3 }),
		Create("""
			var a = new Lookup_aQuaZw().Count;
			var b = new Lookup_aQuaZw().Count;
			var c = new Lookup_aQuaZw().Count;
			var d = new Lookup_aQuaZw().Count;
			var e = new Lookup_aQuaZw().Count;
			var f = new Lookup_aQuaZw().Count;
			var g = new Lookup_aQuaZw().Count;
			var h = new Lookup_aQuaZw().Count;
			var i = new Lookup_aQuaZw().Count;
			var j = new Lookup_aQuaZw().Count;
			var k = new Lookup_aQuaZw().Count;
			var l = new Lookup_aQuaZw().Count;
			var m = new Lookup_aQuaZw().Count;
			var n = new Lookup_aQuaZw().Count;

			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", new int[] { }),
	];
}