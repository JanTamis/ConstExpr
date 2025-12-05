namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitIfStatement - constant condition branch elimination
/// </summary>
[InheritsTests]
public class VisitIfStatementTests : BaseTest
{
	public override string TestMethod => """
		(int, int, int, int) TestMethod(bool condition, int x, int y)
		{
			int a;
			if (true)
				a = 1;
			else
				a = 2;
			
			int b;
			if (false)
				b = 3;
			else
				b = 4;
			
			int c;
			if (condition)
				c = x;
			else
				c = y;
			
			int d;
			if (x > y)
				d = x;
			else
				d = y;
			
			return (a, b, c, d);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var c;
		
		if (condition)
			c = x;
		else
			c = y;
		
		var d;
		
		if (x > y)
			d = x;
		else
			d = y;
		
		return (1, 4, c, d);
		""", Unknown, Unknown, Unknown),
		Create("return (1, 4, 10, 10);", true, 10, 5),
		Create("return (1, 4, 30, 30);", false, 20, 30),
		Create("return (1, 4, 100, 200);", true, 100, 200),
		Create("return (1, 4, 15, 15);", false, 15, 15)
	];
}

