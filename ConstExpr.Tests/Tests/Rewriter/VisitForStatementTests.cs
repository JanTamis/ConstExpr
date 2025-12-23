namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitForStatement - loop unrolling and condition evaluation
/// </summary>
[InheritsTests]
public class VisitForStatementTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		var sum = 0;

		for (var i = 0; i < x; i++)
		{
			sum += i;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 0;", 0),
		Create("return 1;", 1),
		Create("return 3;", 2),
		Create("return 6;", 4),
		Create("return 10;", 5)
	];
}