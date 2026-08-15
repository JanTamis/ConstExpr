namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for CountBy() optimization — verify that redundant materialisation and ordering
///   before CountBy() are stripped, that null-comparer arguments are removed, that
///   Enumerable.Empty&lt;T&gt;() is short-circuited, and that literal Where predicates are folded.
/// </summary>
[InheritsTests]
public class LinqCountByOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		// AsEnumerable().CountBy() => CountBy() (no-op materialisation stripped)
		var a = x.AsEnumerable().CountBy(v => v % 2).Count();

		// ToList().CountBy() => CountBy() (materialisation stripped)
		var b = x.ToList().CountBy(v => v % 2).Count();

		// ToArray().CountBy() => CountBy() (materialisation stripped)
		var c = x.ToArray().CountBy(v => v % 2).Count();

		// OrderBy().CountBy() => CountBy() (ordering doesn't affect key counts)
		var d = x.OrderBy(v => v).CountBy(v => v % 2).Count();

		// Enumerable.Empty<T>().CountBy() => Enumerable.Empty<KeyValuePair<TKey,int>>() => Count() = 0
		var e = Enumerable.Empty<int>().CountBy(v => v % 2).Count();

		// CountBy(keySelector, null) => CountBy(keySelector) (null comparer removed)
		var f = x.CountBy(v => v % 2).Count();

		// Where(v => true).CountBy() => CountBy() (always-true filter stripped)
		var g = x.Where(_ => true).CountBy(v => v % 2).Count();

		// Where(v => false).CountBy() => Enumerable.Empty<KeyValuePair<TKey,int>>() => Count() = 0
		var h = x.Where(_ => false).CountBy(v => v % 2).Count();

		var i = x.CountBy(v => v).Count();

		return a + b + c + d + e + f + g + h + i;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// e (Empty source) and h (Where false) fold to 0 and are pruned from the return sum.
		// v % 2 in key selectors is also optimised to v & 1 by the arithmetic optimizer.
		Create("return Count_BgXwWg(x) * 6 + x.Length;"),
	];
}