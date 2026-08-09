namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitAssignmentExpression - constant assignment folding
/// </summary>
[InheritsTests]
public class VisitAssignmentExpressionTests : BaseTest<Func<int, int, int, (int, int, int)>>
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
		CreateFolded(5, 10, 4),
		CreateFolded(0, 0, 0),
		CreateFolded(-1, 2, -3),
		CreateFolded(10, 9, 7),
		CreateFolded(1, 3, 1)
	];
}