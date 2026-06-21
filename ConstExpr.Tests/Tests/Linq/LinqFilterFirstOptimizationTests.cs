namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for filter-first optimization: source.OrderBy(f).Where(p) => source.Where(p).OrderBy(f)
///   Moving Where before sorting reduces the number of elements to be sorted.
/// </summary>
[InheritsTests]
public class LinqFilterFirstOptimizationTests : BaseTest<Func<int[], IEnumerable<int>>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy(f).Where(p) => Where(p).OrderBy(f)
		var a = x.OrderBy(v => v * 2).Where(v => v > 4);

		// OrderByDescending(f).Where(p) => Where(p).OrderByDescending(f)
		var b = x.OrderByDescending(v => v * 2).Where(v => v > 4);

		return a.Concat(b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Where(v => v > 4).OrderBy(v => v * 2).Concat(x.Where(v => v > 4).OrderByDescending(v => v * 2)))
	];
}