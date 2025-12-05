namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitBinaryExpression - arithmetic/comparison/logical folding
/// </summary>
[InheritsTests]
public class VisitBinaryExpressionTests : BaseTest
{
	public override string TestMethod => """
		(int, int, int, int, int, bool, bool, bool) TestMethod(int x, int y, bool b1, bool b2)
		{
			var a = x + y;
			var b = x - y;
			var c = x * y;
			var d = x / y;
			var e = x % y;
			var f = x > y;
			var g = b1 && b2;
			var h = b1 || b2;
			return (a, b, c, d, e, f, g, h);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown, Unknown),
		Create("return (3, -1, 2, 0, 1, false, false, true);", 1, 2, true, false),
		Create("return (13, 3, 40, 1, 3, true, false, false);", 8, 5, false, false),
		Create("return (25, 5, 150, 1, 5, true, true, true);", 15, 10, true, true),
		Create("return (0, -20, -100, -1, 0, false, false, true);", -10, 10, false, true)
	];
}

