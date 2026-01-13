namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitParenthesizedExpression - unwrap parens to inner expression
/// </summary>
[InheritsTests]
public class VisitParenthesizedExpressionTests : BaseTest<Func<int, int, (int, int, int, int, int, int, int, string)>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = 1 + 2;
		var b = (1 + 2) * 3;
		var c = 5;
		var d = x + y;
		var e = (x + y) * 1;
		var f = 1;
		var g = 1 + 2 + 3;
		var i = $"{x}";
		var j = $"{x}";

		// extra contexts covered by CanRemoveParentheses
		var k = x;

		if (x > y)
		{
			var m = 1;
		}
		else
		{
			var m = 2;
		}

		var t = (x, y);
		var anon = new { a = x };
		var arr = new[]
		{
			x
		};

		void G(int p) { }
		G(x);

		return (a, b, c, d, e, f, g, j);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var d = x + y;
			var e = x + y;
			var j = x.ToString();

			return (3, 9, 5, d, e, 1, 6, j);
			""", Unknown, Unknown),
		Create("return (3, 9, 5, 15, 15, 1, 6, \"10\");", 10, 5),
		Create("return (3, 9, 5, -5, -5, 1, 6, \"-10\");", -10, 5),
		Create("return (3, 9, 5, 0, 0, 1, 6, \"0\");", 0, 0),
		Create("return (3, 9, 5, 42, 42, 1, 6, \"20\");", 20, 22)
	];
}