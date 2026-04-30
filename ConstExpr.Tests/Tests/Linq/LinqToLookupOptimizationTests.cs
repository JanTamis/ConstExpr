using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for ToLookup() optimization - verify redundant materialization removal, ordering removal,
/// identity element-selector removal, Select folding, and Where merging.
/// Note: Unlike ToDictionary, Distinct is NOT stripped because ToLookup groups duplicates
/// and removing Distinct would change the group sizes.
/// </summary>
[InheritsTests]
public class LinqToLookupOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ToLookup_MVa_MQ(x).Count * 7 + ToLookup_sloNHA(x).Count * 2 + x.ToLookup(v => v).Count * 2 + ToLookup_i0qdOA(x).Count + ToLookup_VYmdsA(x).Count + ToLookup_BusWaA(x).Count;"),
		Create("return new Lookup_tdV2Ug().Count * 11 + new Lookup_Z4CZww().Count * 2 + new Lookup_r3gMMA().Count;", new[] { 1, 2, 3 }),
		Create("return new Lookup_Pu_yfg().Count * 14;", new int[] { }),
	];
}