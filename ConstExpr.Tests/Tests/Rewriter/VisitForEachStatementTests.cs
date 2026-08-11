namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitForEachStatement - foreach loop unrolling
/// </summary>
[InheritsTests]
public class VisitForEachStatementTests : BaseTestWithRandomValues<Func<int[], int>>
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
	];
}