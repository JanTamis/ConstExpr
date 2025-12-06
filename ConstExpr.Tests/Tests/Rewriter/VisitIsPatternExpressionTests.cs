namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitIsPatternExpression - constant comparison folding
/// </summary>
[InheritsTests]
public class VisitIsPatternExpressionTests : BaseTest
{
	public override string TestMethod => """
		(bool, bool, bool, bool, bool, bool) TestMethod(int x, int y, object obj, char ch)
		{
			var a = 5 is 5;
			var b = 10 is 20;
			var c = x is 0;
			var d = y is > 0;
			var e = obj is int;
			var f = ch == 'a' || ch == 'e' || ch == 'i' || ch == 'o' || ch == 'u';

			return (a, b, c, d, e, f);
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var c = x is 0;
		var d = y is > 0;
		var e = obj is int;
		var f = ch is 'a' or 'e' or 'i' or 'o' or 'u';
		
		return (true, false, c, d, e, f);
		""", Unknown, Unknown, Unknown, Unknown),
		Create("return (true, false, true, false, true, false);", 0, -5, 42, 'b'),
		Create("return (true, false, false, true, true, false);", 10, 20, 100, 'c'),
		Create("return (true, false, false, true, false, false);", 5, 15, "hello", 'd'),
		Create("return (true, false, false, false, true, true);", -10, -20, 0, 'e')
	];
}

