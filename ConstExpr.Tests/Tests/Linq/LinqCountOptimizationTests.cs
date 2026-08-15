using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Count() optimization - verify that unnecessary operations before Count() are removed
/// </summary>
[InheritsTests]
public class LinqCountOptimizationTests() : BaseTestWithRandomValues<Func<int[], int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.All)
{
	// One of the suite's heaviest random tests: each draw runs the full rewriter over a body of ten-plus
	// LINQ chains, measured at ~0.45 s per draw. Halved from the default 10 - the per-draw cost is set by
	// the body being rewritten, not by the input, so fewer draws is the only knob that moves it.
	protected override int RandomTestCaseCount => 5;

	// Yields 4-5 checked cases at 5 draws (measured); floor leaves margin so a future seed or generator
	// change that starves this class fails loudly instead of quietly checking one.
	protected override int MinRandomTestCaseCount => 3;

	public override string TestMethod => GetString(x =>
	{
		// Where(...).Count() => Count(predicate)
		var a = x.Where(v => v > 3).Count();

		// OrderBy(...).Count() => Count()
		var b = x.OrderBy(v => v).Count();

		// OrderByDescending(...).Count() => Count()
		var c = x.OrderByDescending(v => v).Count();

		// Reverse().Count() => Count()
		var d = x.Reverse().Count();

		// AsEnumerable().Count() => Count()
		var e = x.AsEnumerable().Count();

		// OrderBy().ThenBy().Count() => Count()
		var f = x.OrderBy(v => v).ThenBy(v => v * 2).Count();

		// OrderBy().Where().Count() => Count(predicate)
		var g = x.OrderBy(v => v).Where(v => v > 2).Count();

		// Complex: OrderBy().ThenBy().Reverse().Where().Count() => Count(predicate)
		var h = x.OrderBy(v => v).ThenBy(v => v * 2).Reverse().Where(v => v < 5).Count();

		// Distinct should NOT be optimized (reduces count!)
		var i = x.Distinct().Concat(x).Concat(x).Count();

		// Select should be optimized away
		var j = x.Select(v => v * 2).Count();

		// Multiple chained Where statements should be combined
		var k = x.Where(v => v > 2).Where(v => v < 10).Count();

		// Three chained Where statements
		var l = x.Where(v => v > 1).Where(v => v < 8).Where(v => v % 2 == 0).Count();

		// Where with operations that don't affect count
		var m = x.OrderBy(v => v).Where(v => v > 2).Where(v => v < 10).Count();

		// Complex chain with multiple Where statements
		var n = x.Where(v => v > 1).OrderBy(v => v).Where(v => v < 8).Reverse().Where(v => v % 2 == 0).Count();

		var o = x.GroupBy(v => v % 3).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (x.Length << 3) + (Count_2oJURA(x) << 1) + (Count_kZOLWQ(x) << 1) + Count_R_guEA(x) + Count_h_Rp_w(x) + Count_oTcHpQ(x) + Count_w6J_9Q(x) + Count_tit25Q(x);"),
	];
}