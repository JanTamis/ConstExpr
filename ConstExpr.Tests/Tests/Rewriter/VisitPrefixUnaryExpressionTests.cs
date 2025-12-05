namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitPrefixUnaryExpression - negation, !, ++, -- folding
/// </summary>
[InheritsTests]
public class VisitPrefixUnaryExpressionTests : BaseTest
{
	public override string TestMethod => """
		(int, int, bool, int, int, int, bool) TestMethod(int x, bool b)
		{
			var a = -5;
			var b2 = -(-10);
			var c = -(5);
			var d = !true;
			var e = !false;
			var f = !b;
			var g = -x;
			return (a, b2, d, c, g, 0, f);
			
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var f = !b;
		var g = -x;
		
		return (-5, 10, false, -5, g, 0, f);
		""", Unknown, Unknown),
		Create("return (-5, 10, false, -5, -10, 0, false);", 10, true),
		Create("return (-5, 10, false, -5, 20, 0, true);", -20, false),
		Create("return (-5, 10, false, -5, 0, 0, true);", 0, false),
		Create("return (-5, 10, false, -5, -100, 0, false);", 100, true)
	];
}
