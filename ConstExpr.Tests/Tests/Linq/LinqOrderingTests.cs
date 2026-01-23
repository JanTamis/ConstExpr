namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ ordering operations - verify constant folding for OrderBy, ThenBy, Reverse
/// </summary>
[InheritsTests]
public class LinqOrderingTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy on IEnumerable
		var a = x.OrderBy(v => v).First();

		// OrderByDescending on IEnumerable
		var b = x.OrderByDescending(v => v).First();

		// Order (simplified OrderBy)
		var c = x.Order().Last();

		// OrderDescending
		var d = x.OrderDescending().Last();

		// Reverse on IEnumerable
		var e = x.Reverse().First();

		// Multiple ordering operations
		var f = x.OrderBy(v => v).Take(3).Last();

		// OrderBy with Skip
		var g = x.OrderDescending().Skip(1).First();

		// Combined ordering
		var h = x.Where(v => v > 0).OrderBy(v => v).ElementAt(1);

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Order().First();
			var b = x.OrderDescending().First();
			var c = x.Order().Last();
			var d = x.OrderDescending().Last();
			var e = x.Reverse().First();
			var f = x.Order().Take(3).Last();
			var g = x.OrderDescending().Skip(1).First();
			var h = x.Where(v => v > 0).Order().ElementAt(1);

			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 35;", new[] { 5, 2, 8, 1, 9 }),
	];
}
