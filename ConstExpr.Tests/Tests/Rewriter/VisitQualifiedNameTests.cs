namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitQualifiedName - qualified name evaluation
/// </summary>
[InheritsTests]
public class VisitQualifiedNameTests : BaseTest
{
	public override string TestMethod => """
		string TestMethod()
		{
			return String.Empty;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return \"\";"),
	];
}

