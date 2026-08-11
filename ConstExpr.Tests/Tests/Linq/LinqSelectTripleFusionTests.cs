namespace ConstExpr.Tests.Linq;

/// <summary>
///   Additional Select lambda-fusion scenarios not covered by LinqSelectOptimizationTests.
/// </summary>
[InheritsTests]
public class LinqSelectTripleFusionTests : BaseTestWithRandomValues<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Three chained Select calls should be fused into one
		return x.Select(y => y * 2).Select(z => z + 1).Select(w => w * 3).Sum();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Sum_un6RoA(x);"),
	];
}