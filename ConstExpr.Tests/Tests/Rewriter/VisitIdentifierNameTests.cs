namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitIdentifierName - resolve variable to constant value
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var c = x + 1;
			var e = x + y;

			return (x, x, c, y, e);
			""", Unknown, Unknown),
		Create("return (5, 5, 6, 10, 15);", 5, 10),
		Create("return (100, 100, 101, 200, 300);", 100, 200),
		Create("return (-10, -10, -9, 25, 15);", -10, 25),
		Create("return (0, 0, 1, 0, 0);", 0, 0)
	];
}