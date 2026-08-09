namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitForEachStatement - foreach loop unrolling
/// </summary>
[InheritsTests]
public class VisitForEachStatementTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(items =>
	{
		var sum = 0;

		foreach (var i in items)
		{
			sum += i;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(new[] { 1, 2, 3 }),
		CreateFolded(System.Array.Empty<int>()),
		CreateFolded(new[] { 4, 5, 6 })
	];
}