namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitLiteralExpression - literal passthrough
/// </summary>
[InheritsTests]
public class VisitLiteralExpressionTests : BaseTest<Func<int, double, (int, double, string, char, bool, int)>>
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
		Create((_, _) => (42, 3.14, "hello", 'x', true, 52), [ 10, 1.5 ]),
		Create((_, _) => (42, 3.14, "hello", 'x', true, 142), [ 100, 2.5 ]),
		Create((_, _) => (42, 3.14, "hello", 'x', true, 32), [ -10, 0.0 ]),
		Create((_, _) => (42, 3.14, "hello", 'x', true, 42), [ 0, 5.0 ])
	];
}