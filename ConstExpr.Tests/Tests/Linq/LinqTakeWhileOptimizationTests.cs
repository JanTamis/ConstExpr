namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for TakeWhile() optimization - verify constant predicate handling
/// </summary>
[InheritsTests]
public class LinqTakeWhileOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// TakeWhile(v => true) => source (take everything)
		var a = x.TakeWhile(_ => true).Count();

		// TakeWhile(v => false) => Empty (take nothing)
		var b = x.TakeWhile(_ => false).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Length),
		Create(_ => 3, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ])
	];
}