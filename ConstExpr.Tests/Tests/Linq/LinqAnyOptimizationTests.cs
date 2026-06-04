using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Any() optimization - verify that unnecessary operations before Any() are removed
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Any() => Array.Exists(...) for arrays
		var a = x.Where(v => v > 3).Concat(x).Any() ? 1 : 0;

		// Select(...).Any() => Any()
		var b = x.Select(v => v * 2).Any() ? 1 : 0;

		// Distinct().Any() => Any()
		var c = x.Distinct().Any() ? 1 : 0;

		// OrderBy(...).Any() => Any()
		var d = x.OrderBy(v => v).Any() ? 1 : 0;

		// OrderByDescending(...).Any() => Any()
		var e = x.OrderByDescending(v => v).Any() ? 1 : 0;

		// Reverse().Any() => Any()
		var f = x.Reverse().Any() ? 1 : 0;

		// AsEnumerable().Any() => Any()
		var g = x.AsEnumerable().Any() ? 1 : 0;

		// ToList().Any() => Any()
		var h = x.ToList().Concat(x).Any() ? 1 : 0;

		// ToArray().Any() => Any()
		var i = x.ToArray().Any() ? 1 : 0;

		// Where filters everything out => Array.Exists(...) for arrays
		var j = x.Where(v => v == 100).Any() ? 1 : 0;

		// Should be optimized to Contains
		var k = x.Any(v => v == 2) ? 1 : 0;

		// Direct Any() on array => x.Length > 0
		var l = x.Any() ? 1 : 0;

		var m = x.Append(5).Any(v => v > 3) ? 1 : 0;

		var n = x.Prepend(5).Any(v => v > 3) ? 1 : 0;

		var o = x.DefaultIfEmpty().Any(v => v > 3) ? 1 : 0;

		var p = x.DefaultIfEmpty(5).Any(v => v > 3) ? 1 : 0;

		var q = x.Any(z => (z & 1) == 0) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o + p + q;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (x.Length > 0 ? 9 : 0) + (Any_pfIHsA(x) || x.Length > 0 ? 1 : 0) + (Contains_ug1Wdg(x) ? 1 : 0) + (Contains_Xl5chw(x) ? 1 : 0) + (Any_pfIHsA(x) ? 1 : 0) + 3 + (TensorPrimitives.IsEvenIntegerAny(x) ? 1 : 0);"),
		Create(_ => 16, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 3, [ System.Array.Empty<int>() ]),
	];
}