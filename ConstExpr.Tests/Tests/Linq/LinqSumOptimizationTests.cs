namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Sum() optimization - verify identity lambda removal, Select fusion, and chain optimization
/// </summary>
[InheritsTests]
public class LinqSumOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Sum(v => v) => Sum()
		var a = x.Sum(v => v);

		// Select(selector).Sum() => Sum(selector)
		var b = x.Select(v => v * 2).Sum();

		// OrderBy().Sum() => Sum() (ordering doesn't affect sum)
		var c = x.OrderBy(v => v).Sum();

		// AsEnumerable().ToList().Sum() => Sum()
		var d = x.AsEnumerable().ToList().Sum();

		// Reverse().Sum() => Sum()
		var e = x.Reverse().Sum();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Sum();
			var b = x.Sum(v => v << 1);
			var c = x.Sum();
			var d = x.Sum();
			var e = x.Sum();

			return a + b + c + d + e;
			""", Unknown),
		Create("return 36;", new[] { 1, 2, 3 }),
		Create("return 25;", new[] { 5 }),
	];
}
