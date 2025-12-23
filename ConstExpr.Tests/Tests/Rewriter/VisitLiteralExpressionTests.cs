namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitLiteralExpression - literal passthrough
/// </summary>
[InheritsTests]
public class VisitLiteralExpressionTests : BaseTest<Func<int, double, (int, double, string, char, bool, int)>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		{
			var a = 42;
			var b = 3.14;
			var c = "hello";
			var d = 'x';
			var e = true;
			var f = a + x;
			
			return (a, b, c, d, e, f);
		}
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var f = 42 + x;

			return (42, 3.14, "hello", 'x', true, f);
			""", Unknown, Unknown),
		Create("return (42, 3.14, \"hello\", 'x', true, 52);", 10, 1.5),
		Create("return (42, 3.14, \"hello\", 'x', true, 142);", 100, 2.5),
		Create("return (42, 3.14, \"hello\", 'x', true, 32);", -10, 0.0),
		Create("return (42, 3.14, \"hello\", 'x', true, 42);", 0, 5.0)
	];
}