namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitBinaryExpression - arithmetic/comparison/logical folding
/// </summary>
[InheritsTests]
public class VisitBinaryExpressionTests : BaseTest<Func<int, int, bool, bool, (int, int, int, int, int, bool, bool, bool)>>
{
	public override string TestMethod => GetString((x, y, b1, b2) =>
	{
		var a = x + y;
		var b = x - y;
		var c = x * y;
		var d = x / y;
		var e = x % y;
		var f = x > y;
		var g = b1 && b2;
		var h = b1 || b2;

		return (a, b, c, d, e, f, g, h);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, b1, b2) => (x + y, x - y, x * y, x / y, x % y, x > y, b1 && b2, b1 || b2)),
		CreateFolded(1, 2, true, false),
		CreateFolded(8, 5, false, false),
		CreateFolded(15, 10, true, true),
		CreateFolded(-10, 10, false, true)
	];
}