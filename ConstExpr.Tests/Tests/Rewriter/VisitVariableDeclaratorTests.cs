namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitVariableDeclarator - tracks variables, handles duplicates
/// </summary>
[InheritsTests]
public class VisitVariableDeclaratorTests : BaseTest
{
	public override string TestMethod => """
		(int, int, int, int, int) TestMethod(int x, int y)
		{
			int a = 10;
			int b = a + 5;
			int c = b * 2;
			int d = x + y;
			int e = d - a;
			return (a, b, c, d, e);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var d = x + y;
		var e = d - 10;
		
		return (10, 15, 30, d, e);
		""", Unknown, Unknown),
		Create("return (10, 15, 30, 15, 5);", 10, 5),
		Create("return (10, 15, 30, 25, 15);", 15, 10),
		Create("return (10, 15, 30, 0, -10);", 0, 0),
		Create("return (10, 15, 30, 150, 140);", 100, 50)
	];
}
