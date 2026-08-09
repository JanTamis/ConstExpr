namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Sum() optimization - verify identity lambda removal, Select fusion, and chain optimization
/// </summary>
[InheritsTests]
public class LinqSumOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Sum(v => v) => Sum()
		var a = x.Sum(v => v);

		// Select(selector).Sum() => Sum(selector)
		var b = x.Select(v => v * 2).Concat(x).Sum();

		// OrderBy().Sum() => Sum() (ordering doesn't affect sum)
		var c = x.OrderBy(v => v).Sum();

		// AsEnumerable().ToList().Sum() => Sum()
		var d = x.AsEnumerable().ToList().Sum();

		// Reverse().Sum() => Sum()
		var e = x.Reverse().Sum();

		var f = x.Select(_ => 4).Sum();

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return TensorPrimitives.Sum(x) * 9 + Sum_dcMRsA(x);"),
		CreateFolded(new[] { 1, 2, 3 }),
		CreateFolded(new[] { 5 })
	];
}