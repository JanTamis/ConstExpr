using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Distinct() optimization - verify that unnecessary operations before Distinct() are removed
/// </summary>
[InheritsTests]
public class LinqDistinctOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Distinct().Distinct() => Distinct() (redundant)
		var a = x.Distinct().Distinct().Count();

		// Select(x => x).Distinct() => Distinct() (identity Select)
		var b = x.Select(v => v).Distinct().Count();

		// AsEnumerable().Distinct() => Distinct()
		var c = x.AsEnumerable().Distinct().Count();

		// ToList().Distinct() => Distinct()
		var d = x.ToList().Distinct().Count();

		// ToArray().Distinct() => Distinct()
		var e = x.ToArray().Distinct().Count();

		// AsEnumerable().ToList().Distinct() => Distinct()
		var f = x.AsEnumerable().ToList().Distinct().Count();

		// OrderBy().Distinct().Count() => Distinct().Count() (Count is set-based!)
		var g = x.OrderBy(v => v).Distinct().Count();

		// Reverse().Distinct().Any() => Distinct().Any() (Any is set-based!)
		var h = x.Reverse().Distinct().Any() ? 1 : 0;

		// OrderBy().ThenBy().Distinct().Count() => Distinct().Count() (set-based)
		var i = x.OrderBy(v => v).ThenBy(v => v * 2).Distinct().Count();

		// OrderBy().Distinct().ToList() should NOT optimize (ToList preserves order!)
		var j = x.OrderBy(v => v).Distinct().ToList().Count();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_w6J_9Q(x) * 9 + (x.Length > 0 ? 1 : 0);"),
		Create(_ => 46, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new int[] { } ]),
		Create(_ => 28, [ new[] { 1, 1, 2, 2, 3 } ]), // 3 distinct values
	];
}