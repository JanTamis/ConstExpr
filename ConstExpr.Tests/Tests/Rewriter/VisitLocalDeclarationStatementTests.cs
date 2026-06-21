namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitLocalDeclarationStatement - visit and remove if unused
/// </summary>
[InheritsTests]
public class VisitLocalDeclarationStatementTests : BaseTest<Func<int, int, (int, int, int, int)>>
{
	public override string TestMethod => GetString((x, _) =>
	{
		var a = 1;
		int b = 2, c = 3;
		int unused;

		var d = a + b + c + x;
		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, _) => (1, 2, 3, x + 6)),
		Create((_, _) => (1, 2, 3, 16), [ 10, 5 ]),
		Create((_, _) => (1, 2, 3, 1), [ -5, 0 ]),
		Create((_, _) => (1, 2, 3, 6), [ 0, 100 ]),
		Create((_, _) => (1, 2, 3, 48), [ 42, 7 ])
	];
}