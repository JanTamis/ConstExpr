namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitLocalFunctionStatement - process, inline const local functions
/// </summary>
[InheritsTests]
public class VisitLocalFunctionStatementTests : BaseTest
{
	public override string TestMethod => """
		void TestMethod()
		{
			int Add(int a, int b) => a + b;
			int x = 1;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("")
	];
}

