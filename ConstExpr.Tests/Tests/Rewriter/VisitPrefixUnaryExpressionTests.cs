namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitPrefixUnaryExpression - negation, !, ++, -- folding
/// </summary>
[InheritsTests]
public class VisitPrefixUnaryExpressionTests : BaseTest<Func<int, bool, (int, int, bool, int, int, int, bool)>>
{
	public override string TestMethod => GetString((x, b) =>
	{
		var a = -5;
		var b2 = - -10;
		var c = -5;
		var d = !true;
		var e = !false;
		var f = !b;
		var g = -x;

		return (a, b2, d, c, g, 0, f);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, b) => (-5, 10, false, -5, -x, 0, !b)),
		Create((_, _) => (-5, 10, false, -5, -10, 0, false), [ 10, true ]),
		Create((_, _) => (-5, 10, false, -5, 20, 0, true), [ -20, false ]),
		Create((_, _) => (-5, 10, false, -5, 0, 0, true), [ 0, false ]),
		Create((_, _) => (-5, 10, false, -5, -100, 0, false), [ 100, true ])
	];
}