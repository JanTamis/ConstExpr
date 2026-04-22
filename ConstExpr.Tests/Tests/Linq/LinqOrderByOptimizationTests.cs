namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for OrderBy() optimization - verify identity lambda conversion
/// </summary>
[InheritsTests]
public class LinqOrderByOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy(v => v) => Order()
		var a = x.OrderBy(v => v).First();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Min_BeESfw(x);
			
			return a;
			"""),
		Create("return 1;", new[] { 3, 1, 2 }),
		Create("return 5;", new[] { 5 }),
	];
}

/// <summary>
/// Tests for filter-first optimization: source.OrderBy(f).Where(p) => source.Where(p).OrderBy(f)
/// Moving Where before sorting reduces the number of elements to be sorted.
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
		Create("""
			var a = x.Where(v => v > 4).OrderBy(v => v * 2);
			var b = x.Where(v => v > 4).OrderByDescending(v => v * 2);
			
			return a.Concat(b);
			"""),
	];
}

