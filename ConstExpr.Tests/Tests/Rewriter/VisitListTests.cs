namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitList - visits list elements
/// </summary>
[InheritsTests]
public class VisitListTests : BaseTest<Action>
{
	public override string TestMethod => GetString(() =>
	{
		var a = 1;
		var b = 2;
		return;
		var c = 3;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return;")
	];
}