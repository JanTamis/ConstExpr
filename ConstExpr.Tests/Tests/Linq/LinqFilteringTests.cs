namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ filtering operations - verify constant folding and optimization for Where, Take, Skip, etc.
/// </summary>
[InheritsTests]
public class LinqFilteringTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where with constant predicate that's always true
		var a = x.Where(v => true).Count();

		// Where with constant predicate that's always false
		var b = x.Where(v => false).Count();

		// Where with evaluable predicate
		var c = x.Where(v => v > 2).Count();

		// Take with constant count
		var d = x.Take(3).Sum();

		// Skip with constant count
		var e = x.Skip(2).Sum();

		// TakeLast and SkipLast
		var f = x.TakeLast(2).Sum();
		var g = x.SkipLast(2).Sum();

		// Chained Take and Skip
		var h = x.Skip(1).Take(3).Sum();

		// Where combined with other operations
		var i = x.Where(v => v % 2 == 0).Sum();

		return a + b + c + d + e + f + g + h + i;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Count();
			var b = 0;
			var c = x.Where(v => v > 2).Count();
			var d = x.Take(3).Sum();
			var e = x.Skip(2).Sum();
			var f = x.TakeLast(2).Sum();
			var g = x.SkipLast(2).Sum();
			var h = x.Skip(1).Take(3).Sum();
			var i = x.Where(v => v % 2 == 0).Sum();

			return a + b + c + d + e + f + g + h + i;
			""", Unknown),
		Create("return 56;", new[] { 1, 2, 3, 4, 5 }),
	];
}
