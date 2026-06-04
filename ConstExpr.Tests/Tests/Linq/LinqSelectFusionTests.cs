namespace ConstExpr.Tests.Linq;

/// <summary>
///   Additional Select lambda-fusion scenarios not covered by LinqSelectOptimizationTests.
/// </summary>
[InheritsTests]
public class LinqSelectTripleFusionTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Three chained Select calls should be fused into one
		return x.Select(y => y * 2).Select(z => z + 1).Select(w => w * 3).Sum();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 9;", new[] { 1 }), // ((1*2)+1)*3 = 9
		Create("return 0;", Enumerable.Empty<int>()),
		Create("return 45;", new[] { 1, 2, 3 }) // 9 + ((2*2)+1)*3=15 + ((3*2)+1)*3=21 = 45
	];
}

[InheritsTests]
public class LinqSelectCastToLongTests : BaseTest<Func<IEnumerable<int>, long>>
{
	public override string TestMethod => GetString(x =>
	{
		// Select(y => (long)y) → Cast<long>()
		return x.Select(y => (long) y).Sum();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 6L;", new[] { 1, 2, 3 }),
		Create("return 0L;", Enumerable.Empty<int>()),
		Create("return 42L;", new[] { 42 })
	];
}