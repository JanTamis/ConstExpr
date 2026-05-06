namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for OrderBy() optimization - verify identity lambda conversion
/// </summary>
[InheritsTests]
public class LinqOrderByOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy(v => v) => Order()
		var a = x.OrderBy(v => v).First();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Min_zgmZ3g(x);"),
		Create("return 1;", new[] { 3, 1, 2 }),
		Create("return 5;", new[] { 5 }),
	];
}