namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitQualifiedName - qualified name evaluation
/// </summary>
[InheritsTests]
public class VisitQualifiedNameTests : BaseTestWithRandomValues<Func<string>>
{
	public override string TestMethod => GetString(() => System.String.Empty);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(() => "")
	];
}