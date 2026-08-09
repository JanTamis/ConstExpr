namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitParenthesizedExpression - unwrap parens to inner expression
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var sum = x + y;

			return (3, 9, 5, sum, sum, 1, 6, x.ToString());
		}),
		CreateFolded(10, 5),
		CreateFolded(-10, 5),
		CreateFolded(0, 0),
		CreateFolded(20, 22)
	];
}