using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ToDictionary() optimization - verify redundant materialization removal and empty source optimization.
/// </summary>
[InheritsTests]
public class LinqToDictionaryOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().ToDictionary() => ToDictionary()
		var a = x.AsEnumerable().ToDictionary(v => v).Count;

		// ToList().ToDictionary() => ToDictionary()
		var b = x.ToList().ToDictionary(v => v).Count;

		// ToArray().ToDictionary() => ToDictionary()
		var c = x.ToArray().ToDictionary(v => v).Count;

		// ToList().ToArray().ToDictionary() => ToDictionary()
		var d = x.ToList().ToArray().ToDictionary(v => v).Count;

		// AsEnumerable().ToDictionary(keySelector, elementSelector) => ToDictionary(keySelector, elementSelector)
		var e = x.AsEnumerable().ToDictionary(v => v, v => v * 2).Count;

		// ToList().ToDictionary(keySelector, elementSelector) => ToDictionary(keySelector, elementSelector)
		var f = x.ToList().ToDictionary(v => v, v => v * 2).Count;

		// OrderBy().ToDictionary() => ToDictionary() (ordering doesn't affect dictionary)
		var g = x.OrderBy(v => v).ToDictionary(v => v).Count;

		// OrderByDescending().ToDictionary() => ToDictionary()
		var h = x.OrderByDescending(v => v).ToDictionary(v => v).Count;

		// Reverse().ToDictionary() => ToDictionary()
		var i = x.Reverse().ToDictionary(v => v).Count;

		// Distinct().ToDictionary() => ToDictionary() (keys are already unique in a dictionary)
		var j = x.Distinct().ToDictionary(v => v).Count;

		// Select(x => x).ToDictionary() => ToDictionary() (identity Select is a no-op)
		var k = x.Select(v => v).ToDictionary(v => v).Count;

		// ToDictionary(keySelector, x => x) => ToDictionary(keySelector) (identity element-selector)
		var l = x.ToDictionary(v => v, v => v).Count;

		// Select(selector).ToDictionary(keySelector) => ToDictionary(composedKey, selector)
		var m = x.Select(v => v * 10).ToDictionary(v => v).Count;

		// Where(predicate).ToDictionary(keySelector) => Where(predicate).ToDictionary(keySelector)
		var n = x.Where(v => v > 0).ToDictionary(v => v).Count;

		// Where(p1).Where(p2).ToDictionary(keySelector) => Where(p1 && p2).ToDictionary(keySelector) (merge chained Where)
		var o = x.Where(v => v > 0).Where(v => v < 10).ToDictionary(v => v).Count;

		// DistinctBy(selector).ToDictionary(keySelector) => ToDictionary(keySelector) (when selectors match)
		var p = x.DistinctBy(v => v).ToDictionary(v => v).Count;

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o + p;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ToDictionary_1EegoQ(x).Count * 8 + ToDictionary_sCunBg(x).Count * 2 + ToDictionary_3VBgJQ(x).Count + ToDictionary___qgkw(x).Count + ToDictionary_at7ziQ(x).Count + ToDictionary_XXbqPA(x).Count + ToDictionary_nBFL_A(x).Count + ToDictionary_vxO49g(x).Count;"),
		Create(_ => 48, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ])
	];
}