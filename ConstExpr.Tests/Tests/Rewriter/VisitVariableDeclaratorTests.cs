namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitVariableDeclarator - tracks variables, handles duplicates
/// </summary>
[InheritsTests]
public class VisitVariableDeclaratorTests : BaseTestWithRandomValues<Func<int, int, (int, int, int, int, int)>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = 10;
		var b = a + 5;
		var c = b * 2;
		var d = x + y;
		var e = d - a;

		return (a, b, c, d, e);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var d = x + y;

			return (10, 15, 30, d, d - 10);
		}),
	];
}