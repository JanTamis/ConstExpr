namespace ConstExpr.Tests.Rewriter;

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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => 0, [ 0 ]),
		Create(_ => 0, [ 1 ]),
		Create(_ => 1, [ 2 ]),
		Create(_ => 6, [ 4 ]),
		Create(_ => 10, [ 5 ]),
	];
}