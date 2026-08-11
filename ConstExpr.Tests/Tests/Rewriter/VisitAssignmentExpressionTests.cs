namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitAssignmentExpression - constant assignment folding
/// </summary>
[InheritsTests]
public class VisitAssignmentExpressionTests : BaseTestWithRandomValues<Func<int, int, int, (int, int, int)>>
{
	public override string TestMethod => GetString((a, b, c) =>
	{
		a += 3;
		b -= 2;
		c *= 2;

		return (a, b, c);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b, c) => (a + 3, b - 2, c << 1)),
	];
}