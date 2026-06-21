namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitIdentifierName - resolve variable to constant value
/// </summary>
[InheritsTests]
public class VisitIdentifierNameTests : BaseTest<Func<int, int, (int, int, int, int, int)>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = x;
		var b = a;
		var c = b + 1;
		var d = y;
		var e = a + d;

		return (a, b, c, d, e);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var a = x;
			var d = y;

			return (a, a, a + 1, d, a + d);
		}),
		Create((_, _) => (5, 5, 6, 10, 15), [ 5, 10 ]),
		Create((_, _) => (100, 100, 101, 200, 300), [ 100, 200 ]),
		Create((_, _) => (-10, -10, -9, 25, 15), [ -10, 25 ]),
		Create((_, _) => (0, 0, 1, 0, 0), [ 0, 0 ])
	];
}