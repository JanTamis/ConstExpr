namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Reverse() optimization:
/// - Reverse().Reverse() => original collection
/// - Order().Reverse() => OrderDescending()
/// - OrderBy(k).Reverse() => OrderByDescending(k)
/// - OrderDescending().Reverse() => Order()
/// - OrderByDescending(k).Reverse() => OrderBy(k)
/// </summary>
[InheritsTests]
public class LinqReverseOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Reverse().Reverse() => original
		var a = x.Reverse().Reverse().First();

		// Order().Reverse() => OrderDescending()
		var b = x.Order().Reverse().First();

		// OrderBy(v => v).Reverse() => OrderByDescending(v => v)
		var c = x.OrderBy(v => v).Reverse().First();

		// OrderDescending().Reverse() => Order()
		var d = x.OrderDescending().Reverse().First();

		// OrderByDescending(v => v).Reverse() => OrderBy(v => v)
		var e = x.OrderByDescending(v => v).Reverse().First();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = x[0];
			var b = Max_uzcZ3A(x);
			var c = Max_uzcZ3A(x);
			var d = Min_zgmZ3g(x);
			var e = Min_zgmZ3g(x);
			
			return a + b + c + d + e;
			"""),
		Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 25;", new[] { 5 }),
	];
}
