namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for SkipLast() optimization - verify SkipLast(0) removal
/// </summary>
[InheritsTests]
public class LinqSkipLastOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// SkipLast(0) => source
		var a = x.SkipLast(0).Count();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return x.Length;", Unknown),
		Create("return 3;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

