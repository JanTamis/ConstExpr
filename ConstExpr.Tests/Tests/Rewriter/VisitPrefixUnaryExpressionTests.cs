namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitPrefixUnaryExpression - negation, !, ++, -- folding
/// </summary>
[InheritsTests]
public class VisitPrefixUnaryExpressionTests : BaseTest<Func<int, bool, (int, int, bool, int, int, int, bool)>>
{
	public override string TestMethod => GetString((x, b) =>
	{
		var a = -5;
		var b2 = - -10;
		var c = -5;
		var d = !true;
		var e = !false;
		var f = !b;
		var g = -x;

		return (a, b2, d, c, g, 0, f);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, b) => (-5, 10, false, -5, -x, 0, !b)),
		CreateFolded(10, true),
		CreateFolded(-20, false),
		CreateFolded(0, false),
		CreateFolded(100, true)
	];
}