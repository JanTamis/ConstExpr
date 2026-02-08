namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for SkipWhile() optimization - verify constant predicate handling
/// </summary>
[InheritsTests]
public class LinqSkipWhileOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// SkipWhile(v => false) => source (skip nothing)
		var a = x.SkipWhile(v => false).Count();

		// SkipWhile(v => true) => Empty (skip everything)
		var b = x.SkipWhile(v => true).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return x.Length;", Unknown),
		Create("return 3;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

