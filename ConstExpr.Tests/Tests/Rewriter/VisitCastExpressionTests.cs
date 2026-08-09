namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitCastExpression - cast literal or passthrough
/// </summary>
[InheritsTests]
public class VisitCastExpressionTests : BaseTest<Func<double, int, int, (int, double, char, int)>>
{
	public override string TestMethod => GetString((x, y, z) =>
	{
		var a = (int) x;
		var b = (double) y;
		var c = (char) z;
		var d = (int) 3.14;

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, z) => ((int) x, y, (char) z, 3)),
		CreateFolded(3.14, 42, 65),
		CreateFolded(10.5, 100, 90),
		CreateFolded(-5.8, -10, 32)
	];
}