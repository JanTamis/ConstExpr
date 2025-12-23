namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitConditionalExpression - fold by constant condition, optimizer pass
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var d = condition ? x : y;
			var e = Int32.Max(x, y);

			return (10, 40, 50, d, e);
			""", Unknown, Unknown, Unknown),
		Create("return (10, 40, 50, 100, 100);", true, 100, 50),
		Create("return (10, 40, 50, 25, 75);", false, 25, 75),
		Create("return (10, 40, 50, -10, 20);", true, -10, 20),
		Create("return (10, 40, 50, 15, 15);", false, 15, 15)
	];
}