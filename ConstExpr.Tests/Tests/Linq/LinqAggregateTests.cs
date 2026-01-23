namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ aggregate operations - verify constant folding for aggregate functions
/// </summary>
[InheritsTests]
public class LinqAggregateTests : BaseTest<Func<IEnumerable<int>, long>>
{
	public override string TestMethod => GetString((x) =>
	{
		// Sum of IEnumerable
		var a = x.Sum();

		// Average of IEnumerable
		var b = (int)x.Average();

		// Count should be optimized
		var c = x.Count();

		// Min/Max of IEnumerable
		var d = x.Min();
		var e = x.Max();

		// Any/All with predicates
		var f = x.Any(v => v > 0) ? 1 : 0;
		var g = x.All(v => v > 0) ? 1 : 0;

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Sum();
			var b = (int)x.Average();
			var c = x.Count();
			var d = x.Min();
			var e = x.Max();
			var f = x.Any(v => v > 0) ? 1 : 0;
			var g = x.All(v => v > 0) ? 1 : 0;
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 33L;", new[] { 1, 2, 3, 4, 5 }),
	];
}

