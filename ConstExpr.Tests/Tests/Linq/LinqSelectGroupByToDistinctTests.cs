using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   GroupBy(key).Select(g =&gt; g.Key) → Select(key).Distinct() → DistinctBy(key)
///   Avoids grouping allocation entirely.
/// </summary>
[InheritsTests]
public class LinqSelectGroupByToDistinctTests() : BaseTestWithRandomValues<Func<IEnumerable<int>, int>>(FastMathFlags.Strict, LinqOptimizationMode.Optimize)
{
	public override string TestMethod => GetString(x =>
	{
		return x.GroupBy(v => v % 3).Select(g => g.Key).Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.DistinctBy(v => v % 3).Count()),
	];
}