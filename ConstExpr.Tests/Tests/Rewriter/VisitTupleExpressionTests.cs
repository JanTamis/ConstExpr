namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitTupleExpression - fold to tuple literal or visit args
/// </summary>
[InheritsTests]
public class VisitTupleExpressionTests : BaseTest
{
	public override string TestMethod => """
		((int, int), (int, int), (int, string), (int, int)) TestMethod(int x, int y, string s)
		{
			var t1 = (1, 2);
			var t2 = (1 + 2, 3 * 4);
			var t3 = (x, y);
			var t4 = (x + y, s);
			var t5 = ((x, y), t2);
			var t6 = (10 + 5, x * 2);
			return (t1, t2, t4, t6);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var t4 = (x + y, s);
		var t6 = (15, x << 1);
		
		return ((1, 2), (3, 12), t4, t6);
		""", Unknown, Unknown, Unknown),
		Create("return ((1, 2), (3, 12), (15, \"hello\"), (15, 20));", 10, 5, "hello"),
		Create("return ((1, 2), (3, 12), (15, \"test\"), (15, -10));", -5, 20, "test"),
		Create("return ((1, 2), (3, 12), (0, \"\"), (15, 0));", 0, 0, ""),
		Create("return ((1, 2), (3, 12), (150, \"world\"), (15, 200));", 100, 50, "world")
	];
}
