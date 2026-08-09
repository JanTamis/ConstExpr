namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitVariableDeclaration - visit declarators
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
		CreateFolded(10, 5),
		CreateFolded(-20, 35),
		CreateFolded(0, 0),
		CreateFolded(100, 50)
	];
}