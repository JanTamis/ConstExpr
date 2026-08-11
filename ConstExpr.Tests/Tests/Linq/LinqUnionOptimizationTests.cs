namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Union() optimization - verify empty collection handling and same source removal
/// </summary>
[InheritsTests]
public class LinqUnionOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Union(Enumerable.Empty) => Distinct()
		var a = x.Union([ ]).Count();

		// Union(same) => Distinct()
		var b = x.Union(x).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_w6J_9Q(x) << 1;"),
	];
}