namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ optimization - verify that redundant LINQ chains are simplified
/// </summary>
[InheritsTests]
public class LinqOptimizationTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString((x) =>
	{
		// OrderBy followed by ToArray where result is known
		var a = new[] { 3, 1, 2 }.OrderBy(v => v).ToArray().Length;

		// Select with identity function
		var b = new[] { 1, 2, 3 }.Select(v => v).ToList().Count;

		// Skip(0) should be removed
		var c = new[] { 1, 2, 3 }.Skip(0).Count();

		// Distinct on constant array
		var d = new[] { 1, 1, 2, 2, 3 }.Distinct().Count();

		// Multiple Where clauses that can be combined
		var e = new[] { 1, 2, 3, 4, 5 }
			.Where(v => v > 1)
			.Where(v => v < 5)
			.Count();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = 3;
			var b = 3;
			var c = 3;
			var d = 3;
			var e = 3;

			return 15;
			""", 0),
		Create("return 15;", 10),
	];
}

