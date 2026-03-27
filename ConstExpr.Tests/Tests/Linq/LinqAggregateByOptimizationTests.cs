namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for AggregateBy() optimization — verify that redundant materialization before
/// AggregateBy() is removed, that the empty-source shortcut is applied, and that ordering
/// and filtering are intentionally preserved (they affect which elements end up in each group
/// and the order in which the accumulator is applied).
/// </summary>
[InheritsTests]
public class LinqAggregateByOptimizationTests : BaseTest<Func<int[], int>>
{
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
		Create("""
			var a = x.AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var b = x.AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var c = x.AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var d = x.AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var e = x.OrderBy(v => v + 1).AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var f = x.Where(v => v > 0).AggregateBy(v => v & 1, 0, (acc, v) => acc + v).Count();
			var h = x.AggregateBy(v => v & 1, 0, (acc, v) => acc + v, EqualityComparer<int>.Default).Count();
			var i = x.DistinctBy(v => v & 1).Count();
			var j = x.DistinctBy(v => v & 1).Count();
			
			return a + b + c + d + e + f + h + i + j;
			""", Unknown),
	];
}



