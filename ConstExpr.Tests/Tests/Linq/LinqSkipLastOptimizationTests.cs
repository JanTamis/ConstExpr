namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for SkipLast() optimization - verify SkipLast(0) removal
/// </summary>
[InheritsTests]
public class LinqSkipLastOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// SkipLast(0) => source
		var a = x.SkipLast(0).Count();

		var b = x.SkipLast(1).SkipLast(5).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Length + Count_89mObA(x);"),
		CreateFolded(new[] { 1, 2, 3 }),
		CreateFolded(System.Array.Empty<int>())
	];
}