namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitParenthesizedExpression - unwrap parens to inner expression
/// </summary>
[InheritsTests]
public class VisitParenthesizedExpressionTests : BaseTest 
{
	public override string TestMethod => """
		(int, int, int, int, int, int, int, string, string) TestMethod(int x, int y)
		{
			int a = (1 + 2);
			int b = ((1 + 2) * (3));
			int c = (((5)));
			int d = (((x)) + ((y)));
			int e = ((x + (y))) * (1);
			int f = (((((1)))));
			int g = ((1 + 2)) + ((3));
			string i = $"{(x)}";
			string j = ($"{x}");

			// extra contexts covered by CanRemoveParentheses
			var k = (x);
			if ((x > y)) { var m = 1; } else { var m = 2; }
			var t = ((x, y));
			var anon = new { a = (x) };
			var arr = new[] { (x) };
			void G(int p) { }
			G((x));

			return (a, b, c, d, e, f, g, i, j);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var d = x + y;
		var e = x + y;
		var i = x.ToString();
		var j = x.ToString();
		
		G(x);
		
		return (3, 9, 5, d, e, 1, 6, i, j);
		""", Unknown, Unknown),
		Create("return (3, 9, 5, 15, 15, 1, 6, \"10\", \"10\");", 10, 5),
		Create("return (3, 9, 5, -5, -5, 1, 6, \"-10\", \"-10\");", -10, 5),
		Create("return (3, 9, 5, 0, 0, 1, 6, \"0\", \"0\");", 0, 0),
		Create("return (3, 9, 5, 42, 42, 1, 6, \"20\", \"20\");", 20, 22)
	];
}
