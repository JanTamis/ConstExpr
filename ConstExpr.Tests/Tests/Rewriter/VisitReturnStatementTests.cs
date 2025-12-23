namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitReturnStatement - constant return value folding
/// </summary>
[InheritsTests]
public class VisitReturnStatementTests : BaseTest<Func<int>>
{
	public override string TestMethod => GetString(() => 1 + 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return 3;")
	];
}