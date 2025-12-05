namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitExpressionStatement - visit expression in statement
/// </summary>
[InheritsTests]
public class VisitExpressionStatementTests : BaseTest
{
	public override string TestMethod => """
		int TestMethod(int x)
		{
			x++;
			x--;
			
			return x;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 6;", 6),
	];
}

