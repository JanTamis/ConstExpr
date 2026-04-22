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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Count_4OhS1w(x);
			var b = Count_4OhS1w(x);
			
			return a + b;
			"""),
		Create("return 6;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

