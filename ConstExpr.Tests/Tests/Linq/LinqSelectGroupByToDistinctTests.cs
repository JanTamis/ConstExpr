using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   GroupBy(key).Select(g =&gt; g.Key) → Select(key).Distinct() → DistinctBy(key)
///   Avoids grouping allocation entirely.
/// </summary>
[InheritsTests]
public class LinqSelectGroupByToDistinctTests() : BaseTest<Func<IEnumerable<int>, int>>(FastMathFlags.Strict, LinqOptimizationMode.Optimize)
{
	public override string TestMethod => GetString(x =>
	{
		return x.GroupBy(v => v % 3).Select(g => g.Key).Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.DistinctBy(v => v % 3).Count()),
		Create(_ => 3, [ new[] { 1, 2, 3, 4, 5, 6 } ]),
		Create(_ => 1, [ new[] { 3, 6, 9 } ]),
		Create(_ => 0, [ Enumerable.Empty<int>() ])
	];
}

[InheritsTests]
public class LinqSelectGroupByFirstCountTests() : BaseTest<Func<IEnumerable<int>, int>>(FastMathFlags.Strict, LinqOptimizationMode.Optimize)
{
	public override string TestMethod => GetString(x =>
	{
		// GroupBy(k).Select(g => g.First()).Count() is also optimized to DistinctBy(k).Count()
		// because Count() only cares about the number of groups, not their elements.
		return x.GroupBy(v => v % 3).Select(g => g.First()).Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.DistinctBy(v => v % 3).Count()),
		Create(_ => 3, [ new[] { 1, 2, 3, 4, 5, 6 } ]),
		Create(_ => 1, [ new[] { 3, 6, 9 } ])
	];
}