namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for arr.Where(p).ToArray() => Array.FindAll(arr, p)
/// </summary>
[InheritsTests]
public class LinqWhereToArrayOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// arr.Where(p).ToArray() => Array.FindAll(arr, p)
		var a = x.Where(v => v > 2).ToArray().Length;

		// Redundant materializing before Where should be stripped transparently
		var b = x.ToList().Where(v => v > 2).ToArray().Length;

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Array.FindAll(x, v => v > 2).Length << 1;"),
	];
}