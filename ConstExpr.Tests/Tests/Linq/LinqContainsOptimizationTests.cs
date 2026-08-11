namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() optimization - verify that unnecessary operations before Contains() are removed
///   and that Contains is optimized for specific collection types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains
		var a = x.Contains(3) ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains(3) ? 1 : 0;

		// OrderBy(...).Contains() => Contains()
		var c = x.OrderBy(v => v).Contains(3) ? 1 : 0;

		// OrderByDescending(...).Contains() => Contains()
		var d = x.OrderByDescending(v => v).Contains(3) ? 1 : 0;

		// Reverse().Contains() => Contains()
		var e = x.Reverse().Contains(3) ? 1 : 0;

		// AsEnumerable().Contains() => Contains()
		var f = x.AsEnumerable().Contains(3) ? 1 : 0;

		// ToList().Contains() => Contains()
		var g = x.ToList().Contains(3) ? 1 : 0;

		// ToArray().Contains() => Contains()
		var h = x.ToArray().Contains(3) ? 1 : 0;

		// Chained operations: Distinct().OrderBy().Reverse().Contains() => Contains()
		var i = x.Distinct().OrderBy(v => v).Reverse().Contains(3) ? 1 : 0;

		// Select(...).Contains() => Any(...); v * 2 == 6 does NOT fold further to v == 3 (unlike
		// EqualsIntegerAdditiveTest's +/- cases) — multiply-by-even-c isn't a bijection mod 2^32
		// (v = int.MinValue + 3 also satisfies v * 2 == 6 via wraparound), so the equality-isolation
		// strategy correctly declines and this stays a standalone Array.Exists predicate below.
		var j = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Any(...)
		var k = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var l = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Unsafe.BitCast<bool, byte>(Contains__KFndQ(x)) * 10 + Unsafe.BitCast<bool, byte>(Array.Exists(x, v => v << 1 == 6)) + Unsafe.BitCast<bool, byte>(Contains_H_mKqw(x));"),
	];
}