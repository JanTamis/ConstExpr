namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for OrderByDescending() optimization - verify identity lambda conversion
/// </summary>
[InheritsTests]
public class LinqOrderByDescendingOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderByDescending(v => v) => OrderDescending()
		var a = x.OrderByDescending(v => v).First();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Max();

			return a;
			""", Unknown),
		Create("return 3;", new[] { 3, 1, 2 }),
		Create("return 5;", new[] { 5 }),
	];
}