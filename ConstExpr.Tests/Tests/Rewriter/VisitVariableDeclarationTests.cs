namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitVariableDeclaration - visit declarators
/// </summary>
[InheritsTests]
public class VisitVariableDeclarationTests : BaseTest<Func<int, int, (int, int, int, int, int)>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		int a = 1, b = 2, c = 3;
		var d = x + y;
		int e = x * 2, f = y - 1;

		return (a, b, c, d, e);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => (1, 2, 3, x + y, x << 1)),
		Create((_, _) => (1, 2, 3, 15, 20), [ 10, 5 ]),
		Create((_, _) => (1, 2, 3, 15, -40), [ -20, 35 ]),
		Create((_, _) => (1, 2, 3, 0, 0), [ 0, 0 ]),
		Create((_, _) => (1, 2, 3, 150, 200), [ 100, 50 ])
	];
}