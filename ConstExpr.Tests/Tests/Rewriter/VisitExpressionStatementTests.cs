namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitExpressionStatement - visit expression in statement
/// </summary>
[InheritsTests]
public class VisitExpressionStatementTests : BaseTest<Func<int, int>>
{
	// Using lambda expression for type-safe, refactorable test method definition
	public override string TestMethod => GetString(x =>
	{
		x++;
		x--;

		return x;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 6;", 6)
	];
}