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
		Create((x, y) => (x, x, x + 1, y, x + y)),
		CreateFolded(5, 10),
		CreateFolded(100, 200),
		CreateFolded(-10, 25),
		CreateFolded(0, 0)
	];
}