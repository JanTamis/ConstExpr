namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitAssignmentExpression - constant assignment folding
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			a += 3;
			b -= 2;
			c <<= 1;

			return (a, b, c);
			""", Unknown, Unknown, Unknown),
		Create("return (8, 8, 8);", 5, 10, 4),
		Create("return (3, -2, 0);", 0, 0, 0),
		Create("return (2, 0, -6);", -1, 2, -3),
		Create("return (13, 7, 14);", 10, 9, 7),
		Create("return (4, 1, 2);", 1, 3, 1)
	];
}