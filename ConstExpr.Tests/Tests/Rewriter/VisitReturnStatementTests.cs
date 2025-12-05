namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitReturnStatement - constant return value folding
/// </summary>
[InheritsTests]
public class VisitReturnStatementTests : BaseTest
{
	public override string TestMethod => """
		int TestMethod()
		{
			return 1 + 2;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return 3;")
	];
}

