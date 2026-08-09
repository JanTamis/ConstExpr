namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for OrderByDescending() optimization - verify identity lambda conversion
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return TensorPrimitives.Max(x);"),
		CreateFolded(new[] { 3, 1, 2 }),
		CreateFolded(new[] { 5 })
	];
}