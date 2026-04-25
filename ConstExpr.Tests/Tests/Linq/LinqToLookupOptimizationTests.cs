namespace ConstExpr.Tests.Linq;

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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = ToLookup_MVa_MQ(x).Count;
			var b = ToLookup_MVa_MQ(x).Count;
			var c = ToLookup_MVa_MQ(x).Count;
			var d = ToLookup_MVa_MQ(x).Count;
			var e = ToLookup_sloNHA(x).Count;
			var f = ToLookup_sloNHA(x).Count;
			var g = ToLookup_MVa_MQ(x).Count;
			var h = ToLookup_MVa_MQ(x).Count;
			var i = ToLookup_MVa_MQ(x).Count;
			var j = x.ToLookup(v => v).Count;
			var k = x.ToLookup(v => v).Count;
			var l = ToLookup_i0qdOA(x).Count;
			var m = ToLookup_VYmdsA(x).Count;
			var n = ToLookup_BusWaA(x).Count;
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			"""),
		Create("""
			var a = new Lookup_tdV2Ug().Count;
			var b = new Lookup_tdV2Ug().Count;
			var c = new Lookup_tdV2Ug().Count;
			var d = new Lookup_tdV2Ug().Count;
			var e = new Lookup_Z4CZww().Count;
			var f = new Lookup_Z4CZww().Count;
			var g = new Lookup_tdV2Ug().Count;
			var h = new Lookup_tdV2Ug().Count;
			var i = new Lookup_tdV2Ug().Count;
			var j = new Lookup_tdV2Ug().Count;
			var k = new Lookup_tdV2Ug().Count;
			var l = new Lookup_r3gMMA().Count;
			var m = new Lookup_tdV2Ug().Count;
			var n = new Lookup_tdV2Ug().Count;
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", new[] { 1, 2, 3 }),
		Create("""
			var a = new Lookup_Pu_yfg().Count;
			var b = new Lookup_Pu_yfg().Count;
			var c = new Lookup_Pu_yfg().Count;
			var d = new Lookup_Pu_yfg().Count;
			var e = new Lookup_Pu_yfg().Count;
			var f = new Lookup_Pu_yfg().Count;
			var g = new Lookup_Pu_yfg().Count;
			var h = new Lookup_Pu_yfg().Count;
			var i = new Lookup_Pu_yfg().Count;
			var j = new Lookup_Pu_yfg().Count;
			var k = new Lookup_Pu_yfg().Count;
			var l = new Lookup_Pu_yfg().Count;
			var m = new Lookup_Pu_yfg().Count;
			var n = new Lookup_Pu_yfg().Count;

			return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
			""", new int[] { }),
	];
}