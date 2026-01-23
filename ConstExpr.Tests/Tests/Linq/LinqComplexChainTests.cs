namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for complex LINQ chains - verify constant folding for multiple chained operations
/// </summary>
[InheritsTests]
public class LinqComplexChainTests : BaseTest<Func<IEnumerable<double>, double>>
{
	public override string TestMethod => GetString(x =>
	{
		// Long chain with multiple operations
		var a = x.Where(v => v % 2 == 0)
			.Select(v => v * 2)
			.OrderByDescending(v => v)
			.Take(3)
			.Sum();

		// Complex filtering and projection
		var b = x.Select(v => v * 2)
			.Where(v => v > 5)
			.Select(v => v + 1)
			.Sum();

		// Nested Select operations (should be flattened)
		var c = x.Select(v => v + 1)
			.Select(v => v * 2)
			.Select(v => v - 1)
			.Sum();

		// Multiple Where clauses (should be combined)
		var d = x.Where(v => v > 2)
			.Where(v => v < 8)
			.Where(v => v % 2 == 0)
			.Count();

		// Combination of Take, Skip, and aggregation
		var e = x.Skip(2)
			.Take(5)
			.Where(v => v % 2 == 1)
			.Sum();

		// OrderBy followed by GroupBy
		var f = x.OrderBy(v => v)
			.GroupBy(v => v % 2)
			.Count();

		// Distinct with Select
		var g = x.Concat(x)
			.Select(v => v % 3)
			.Distinct()
			.Count();

		// Union with projection
		var h = x.Select(v => v * 2)
			.Union(new[] { 4d, 5d, 6d }.Select(v => v * 2))
			.Count();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Where(v => v % 2 == 0).Select(v => v * 2).OrderDescending().Take(3).Sum();
			var b = x.Select(v => v * 2).Where(v => v > 5).Select(v => v + 1).Sum();
			var c = x.Select(v => v + 1).Select(v => v * 2).Select(v => v - 1).Sum();
			var d = x.Where(v => v > 2 && v < 8 && v % 2 == 0).Count();
			var e = x.Skip(2).Take(5).Where(v => v % 2 == 1).Sum();
			var f = x.Order().GroupBy(v => v % 2).Count();
			var g = x.Concat(x).Select(v => v % 3).Distinct().Count();
			var h = x.Select(v => v * 2).Union(new[]
			{
				4,
				5,
				6
			}.Select(v => v * 2)).Count();

			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 111;", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }),
	];
}
