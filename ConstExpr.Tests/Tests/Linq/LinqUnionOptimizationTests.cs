namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Union() optimization - verify empty collection handling and same source removal
/// </summary>
[InheritsTests]
public class LinqUnionOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Union(Enumerable.Empty) => Distinct()
		var a = x.Union(Enumerable.Empty<int>()).Count();

		// Union(same) => Distinct()
		var b = x.Union(x).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Distinct().Count();
			var b = x.Distinct().Count();

			return a + b;
			""", Unknown),
		Create("return 6;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

