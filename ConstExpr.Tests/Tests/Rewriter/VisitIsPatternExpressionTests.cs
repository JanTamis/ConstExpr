namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitIsPatternExpression - constant comparison folding
/// </summary>
[InheritsTests]
public class VisitIsPatternExpressionTests : BaseTest
{
	public override string TestMethod => """
		(bool, bool, bool, bool, bool) TestMethod(int x, int y, object obj)
		{
			var a = 5 is 5;
			var b = 10 is 20;
			var c = x is 0;
			var d = y is > 0;
			var e = obj is int;
			
			return (a, b, c, d, e);
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var c = x is 0;
		var d = y is> 0;
		var e = obj is int;
		
		return (true, false, c, d, e);
		""", Unknown, Unknown, Unknown),
		Create("return (true, false, true, false, true);", 0, -5, 42),
		Create("return (true, false, false, true, true);", 10, 20, 100),
		Create("return (true, false, false, true, false);", 5, 15, "hello"),
		Create("return (true, false, false, false, true);", -10, -20, 0)
	];
}

