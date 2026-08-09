namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitConditionalExpression - fold by constant condition, optimizer pass
/// </summary>
[InheritsTests]
public class VisitConditionalExpressionTests : BaseTest<Func<bool, int, int, (int, int, int, int, int)>>
{
	public override string TestMethod => GetString((condition, x, y) =>
	{
		var a = true ? 10 : 20;
		var b = false ? 30 : 40;
		var c = 5 > 3 ? 50 : 60;
		var d = condition ? x : y;
		var e = x > y ? x : y;

		return (a, b, c, d, e);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((condition, x, y) => (10, 40, 50, condition ? x : y, Int32.Max(x, y))),
		CreateFolded(true, 100, 50),
		CreateFolded(false, 25, 75),
		CreateFolded(true, -10, 20),
		CreateFolded(false, 15, 15)
	];
}