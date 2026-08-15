namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Where() optimization - verify constant folding and combining of Where clauses
/// </summary>
[InheritsTests]
public class LinqWhereOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		// Where(v => true) - should be removed entirely
		var a = x.Where(_ => true).Count();

		// Where(v => false) - should be replaced with Empty
		var b = x.Where(_ => false).Count();

		// Consecutive Where calls with same parameter - should combine with &&
		var c = x.Where(v => v > 1).Where(v => v < 5).Count();

		// Consecutive Where calls with different parameters - should still combine
		var d = x.Where(v => v > 0).Where(r => r < 10).Count();

		// Multiple consecutive Where calls - should combine all
		var e = x.Where(v => v > 0).Where(v => v < 10).Where(v => v % 2 == 0).Count();

		// Where(v => true) in chain - should be removed
		var f = x.Where(_ => true).Where(v => v > 3).Count();

		// Where(v => false) in chain - result should be empty
		var g = x.Where(v => v > 1).Where(_ => false).Count();

		// Complex predicates
		var h = x.Where(v => v > 0 && v < 100).Where(v => v % 2 == 0).Count();

		var i = x.Where(x => x is double).Sum();

		return a + b + c + d + e + f + g + h + i;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Length + Count_D8X0kQ(x) + Count_2IYd7A(x) + Count_Vq7dCg(x) + Count_R_guEA(x) + Count_yTPAKg(x);"),
	];
}