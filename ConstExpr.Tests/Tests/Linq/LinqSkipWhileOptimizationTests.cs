namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for SkipWhile() optimization - verify constant predicate handling
/// </summary>
[InheritsTests]
public class LinqSkipWhileOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// SkipWhile(v => false) => source (skip nothing)
		var a = x.SkipWhile(_ => false).Count();

		// SkipWhile(v => true) => Empty (skip everything)
		var b = x.SkipWhile(_ => true).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Length),
		Create(_ => 3, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ])
	];
}