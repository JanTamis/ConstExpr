namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitForStatement - loop unrolling and condition evaluation
/// </summary>
[InheritsTests]
public class VisitForStatementTests : BaseTestWithRandomValues<Func<int, int>>
{

	protected override int MaxRandomMagnitudeBits => 5;
	public override string TestMethod => GetString(x =>
	{
		var sum = 0;

		for (var i = 0; i < x; i++)
		{
			sum += i;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(0),
		CreateFolded(1),
		CreateFolded(2),
		CreateFolded(4),
		CreateFolded(5)
	];
}