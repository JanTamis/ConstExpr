namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitList - visits list elements
/// </summary>
[InheritsTests]
public class VisitListTests : BaseTest
{
	public override string TestMethod => """
		void TestMethod()
		{
			int a = 1;
			int b = 2;
			return;
			int c = 3;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return;")
	];
}

