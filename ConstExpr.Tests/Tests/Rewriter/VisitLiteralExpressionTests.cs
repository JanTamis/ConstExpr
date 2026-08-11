namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitLiteralExpression - literal passthrough
/// </summary>
[InheritsTests]
public class VisitLiteralExpressionTests : BaseTestWithRandomValues<Func<int, double, (int, double, string, char, bool, int)>>
{
	public override string TestMethod => GetString((x, _) =>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, _) => (42, 3.14, "hello", 'x', true, x + 42)),
	];
}