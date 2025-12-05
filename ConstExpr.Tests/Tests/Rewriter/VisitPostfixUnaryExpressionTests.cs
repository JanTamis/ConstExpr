namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitPostfixUnaryExpression - ++ and -- folding
/// </summary>
[InheritsTests]
public class VisitPostfixUnaryExpressionTests : BaseTest
{
	public override string TestMethod => """
		int TestMethod(int x)
		{
			var a = x;
			var b = a++;
			var c = a--;
			
			return a;
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 7;", 7),
		Create("return 1;", 1),
		Create("return -1;", -1),
		Create("return 0;", 0),
	];
}

