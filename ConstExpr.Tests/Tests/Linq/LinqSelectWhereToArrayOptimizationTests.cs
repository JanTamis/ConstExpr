namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for arr.Select(f).Where(p).ToArray() => Array.FindAll(arr, x => p(f(x)))
/// </summary>
[InheritsTests]
public class LinqSelectWhereToArrayOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// arr.Select(f).Where(p).ToArray() => Array.FindAll(arr, x => p(f(x)))
		var a = x.Select(v => v * 2).Where(v => v > 4).ToArray().Length;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Array.FindAll(x, v => v << 1 > 4).Length;"),
		Create(_ => 3, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
	];
}