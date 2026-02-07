namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Skip() optimization - verify Skip(0) removal
/// </summary>
[InheritsTests]
public class LinqSkipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(0) => source
		var a = x.Skip(0).Count();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return a.Length;", Unknown),
		Create("return 3;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

