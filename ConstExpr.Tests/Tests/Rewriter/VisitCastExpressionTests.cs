namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitCastExpression - cast literal or passthrough
/// </summary>
[InheritsTests]
public class VisitCastExpressionTests : BaseTest<Func<double, int, int, (int, double, char, int)>>
{
	public override string TestMethod => GetString((x, y, z) =>
	{
		var a = (int)x;
		var b = (double)y;
		var c = (char)z;
		var d = (int)3.14;

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, z) => ((int)x, (double)y, (char)z, 3)),
		Create((_, _, _) => (3, 42.0, 'A', 3), [ 3.14, 42, 65 ]),
		Create((_, _, _) => (10, 100.0, 'Z', 3), [ 10.5, 100, 90 ]),
		Create((_, _, _) => (-5, -10.0, ' ', 3), [ -5.8, -10, 32 ])
	];
}