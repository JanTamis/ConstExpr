namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Distinct() optimization - verify that unnecessary operations before Distinct() are removed
/// </summary>
[InheritsTests]
public class LinqDistinctOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		Create("return Count_w6J_9Q(x) * 9 + Unsafe.BitCast<bool, byte>(x.Length > 0);"),
	];
}