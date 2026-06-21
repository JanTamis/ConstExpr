using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() optimization - verify that unnecessary operations before Contains() are removed
///   and that Contains is optimized for specific collection types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
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

		// Select(...).Contains() => Any(...)
		var j = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Any(...)
		var k = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var l = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (Contains_L_btow(x) ? 11 : 0) + (Contains_ug1Wdg(x) ? 1 : 0);"),
		Create(_ => 11, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
		Create(_ => 0, [ new[] { 1, 2, 4, 5, 6 } ]) // No 3, all tests fail
	];
}