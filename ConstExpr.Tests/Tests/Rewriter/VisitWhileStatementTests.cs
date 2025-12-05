namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitWhileStatement - loop unrolling with constant condition
/// </summary>
[InheritsTests]
public class VisitWhileStatementTests : BaseTest
{
	public override string TestMethod => """
		(int, int, int, int) TestMethod(int limit, bool condition)
		{
			int a = 0;
			while (false)
			{
				a++;
			}
			
			int b = 10;
			while (true)
			{
				b++;
				break;
			}
			
			int c = 0;
			while (c < limit)
			{
				c++;
			}
			
			int d = 5;
			while (condition)
			{
				d--;
				break;
			}
			
			return (a, b, c, d);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var c = 0;
		
		while (c < limit)
		{
			c++;
		}
		
		var d = 5;
		
		while (condition)
		{
			d--;
		
			break;
		}
		
		return (0, 11, c, d);
		""", Unknown, Unknown),
		Create("return (0, 11, 3, 4);", 3, true),
		Create("return (0, 11, 0, 4);", 0, true),
		Create("return (0, 11, 5, 5);", 5, false),
		Create("return (0, 11, 10, 4);", 10, true)
	];
}
