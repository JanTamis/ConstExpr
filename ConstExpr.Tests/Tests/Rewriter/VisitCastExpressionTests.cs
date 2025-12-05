namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitCastExpression - cast literal or passthrough
/// </summary>
[InheritsTests]
public class VisitCastExpressionTests : BaseTest
{
	public override string TestMethod => """
		(int, double, char, int) TestMethod(double x, int y, int z)
		{
			int a = (int)x;
			double b = (double)y;
			char c = (char)z;
			int d = (int)3.14;
			return (a, b, c, d);
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var a = (int)x;
		var b = (double)y;
		var c = (char)z;

		return (a, b, c, 3);
		""", Unknown, Unknown, Unknown),
		Create("return (3, 42.0, 'A', 3);", 3.14, 42, 65),
		Create("return (10, 100.0, 'Z', 3);", 10.5, 100, 90),
		Create("return (-5, -10.0, ' ', 3);", -5.8, -10, 32)
	];
}

