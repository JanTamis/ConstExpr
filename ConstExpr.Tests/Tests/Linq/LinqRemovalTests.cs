namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ function removal - verify that unnecessary LINQ operations are eliminated
/// </summary>
[InheritsTests]
public class LinqRemovalTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString((x) =>
	{
		// Where with always-true condition should be removed
		var a = new[] { 1, 2, 3 }.Where(v => true).Count();

		// Select that doesn't transform should be optimized
		var b = new[] { 1, 2, 3 }.Select(v => v).Count();

		// FirstOrDefault with single element
		var c = new[] { x }.FirstOrDefault();

		// Chained Where filters
		var d = new[] { 1, 2, 3, 4, 5 }.Where(v => v > 0).Where(v => v < 10).Count();

		// Take with length greater than array
		var e = new[] { 1, 2, 3 }.Take(10).Count();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = 3;
			var b = 3;
			var c = 5;
			var d = 5;
			var e = 3;

			return 19;
			""", 5),
		Create("return 16;", 2),
		Create("return 17;", 10),
	];
}

