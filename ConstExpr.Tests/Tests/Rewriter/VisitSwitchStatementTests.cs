namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitSwitchStatement - constant folding of switch expression
/// </summary>
[InheritsTests]
public class VisitSwitchStatementTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(value =>
	{
		var result = 0;

		switch (value)
		{
			case 1:
				result = 10;
				break;
			case 2:
				result = 20;
				break;
			default:
				result = 30;
				break;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 10;", 1),
		Create("return 20;", 2),
		Create("return 30;", 3)
	];
}