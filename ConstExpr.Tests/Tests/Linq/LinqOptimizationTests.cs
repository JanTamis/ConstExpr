namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ optimization - verify that redundant LINQ chains are simplified
/// </summary>
[InheritsTests]
public class LinqOptimizationTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy followed by ToArray where result is known
		var a = x.OrderBy(v => v).ToArray().Length;

		// Select with identity function
		var b = x.Select(v => v).ToList().Count;

		// Skip(0) should be removed
		var c = x.Skip(0).Count();

		// Distinct on constant array
		var d = x.Distinct().Count();

		// Multiple Where clauses that can be combined
		var e = x
			.Where(v => v > 1)
			.Where(v => v < 5)
			.Count();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Order().Count();
			var b = x.Count();
			var c = x.Count();
			var d = x.Distinct().Count();
			var e = x.Where(v => v > 1 && v < 5).Count();
			
			return a + b + c + d + e;
			""", Unknown),
		Create("return 15;", 10),
	];
}

