namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ grouping operations - verify constant folding for GroupBy, ToLookup
/// </summary>
[InheritsTests]
public class LinqGroupingTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// GroupBy on IEnumerable
		var a = x.GroupBy(v => v % 2).Count();

		// GroupBy with element selector
		var b = x.GroupBy(v => v % 2, v => v * 2).First().Count();

		// GroupBy with result selector
		var c = x.GroupBy(v => v % 3, (key, group) => group.Sum()).Max();

		// Chunk operation
		var d = x.Chunk(3).Count();

		// Partition operation using GroupBy
		var e = x.GroupBy(v => v > 3).Count();

		// Count of elements in first group
		var f = x.GroupBy(v => v % 2).First().Count();

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.GroupBy(v => v % 2).Count();
			var b = x.GroupBy(v => v % 2, v => v * 2).First().Count();
			var c = x.GroupBy(v => v % 3, (key, group) => group.Sum()).Max();
			var d = x.Chunk(3).Count();
			var e = x.GroupBy(v => v > 3).Count();
			var f = x.GroupBy(v => v % 2).First().Count();

			return a + b + c + d + e + f;
			""", Unknown),
		Create("return 17;", new[] { 1, 2, 3, 4, 5, 6 }),
	];
}
