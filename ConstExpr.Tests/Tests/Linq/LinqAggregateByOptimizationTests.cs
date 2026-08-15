namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for AggregateBy() optimization — verify that redundant materialization before
///   AggregateBy() is removed, that the empty-source shortcut is applied, and that ordering
///   and filtering are intentionally preserved (they affect which elements end up in each group
///   and the order in which the accumulator is applied).
/// </summary>
[InheritsTests]
public class LinqAggregateByOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		// AsEnumerable().AggregateBy() => AggregateBy() (strips no-op materialization)
		var a = x.AsEnumerable().AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// ToList().AggregateBy() => AggregateBy()
		var b = x.ToList().AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// ToArray().AggregateBy() => AggregateBy()
		var c = x.ToArray().AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// ToList().AsEnumerable().AggregateBy() => AggregateBy() (chain of materializations stripped)
		var d = x.ToList().AsEnumerable().AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// OrderBy().AggregateBy() — ordering is NOT stripped (order matters for accumulation within groups)
		var e = x.OrderBy(v => v + 1).AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// Where().AggregateBy() — filter is NOT stripped (changes which elements are grouped)
		var f = x.Where(v => v > 0).AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// Enumerable.Empty<T>().AggregateBy() => Enumerable.Empty<KeyValuePair<TKey,TAccumulate>>() => Count() = 0
		var g = Enumerable.Empty<int>().AggregateBy(v => v % 2, 0, (acc, v) => acc + v).Count();

		// AsEnumerable() before 4-arg overload (with key comparer) is also stripped
		var h = x.AsEnumerable().AggregateBy(v => v % 2, 0, (acc, v) => acc + v, EqualityComparer<int>.Default).Count();

		// AggregateBy(keySelector, 0, (acc, _) => acc + 1) => CountBy(keySelector)
		var i = x.AggregateBy(v => v % 2, 0, (acc, _) => acc + 1).Count();

		// AggregateBy(keySelector, 0, (acc, _) => acc + 1, comparer) => CountBy(keySelector, comparer)
		var j = x.AggregateBy(v => v % 2, 0, (acc, _) => acc + 1, EqualityComparer<int>.Default).Count();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_X_e_aw(x) * 5 + Count_kVGBzg(x) + Count_WP_tfQ(x) + x.CountBy(v => v % 2).Count() + Count_BgXwWg(x);")
	];
}