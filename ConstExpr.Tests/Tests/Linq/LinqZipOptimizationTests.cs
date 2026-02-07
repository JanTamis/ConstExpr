namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Zip() optimization - verify empty collection handling
/// </summary>
[InheritsTests]
public class LinqZipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Zip with empty => empty
		var a = x.Zip(Enumerable.Empty<int>()).Count();

		// Empty.Zip(collection) => empty
		var b = Enumerable.Empty<int>().Zip(x).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return 0;", Unknown),
		Create("return 0;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

