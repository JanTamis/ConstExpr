namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitQualifiedName - qualified name evaluation
/// </summary>
[InheritsTests]
public class VisitQualifiedNameTests : BaseTest<Func<string>>
{
	public override string TestMethod => GetString(() => System.String.Empty);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return \"\";")
	];
}