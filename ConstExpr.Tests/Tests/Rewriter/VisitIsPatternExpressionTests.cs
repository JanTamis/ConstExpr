namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitIsPatternExpression - constant comparison folding
/// </summary>
[InheritsTests]
public class VisitIsPatternExpressionTests : BaseTest<Func<int, int, object, char, bool[]>>
{
	public override string TestMethod => GetString((x, y, obj, ch) =>
	{
		var a = 5 is 5;
		var b = 10 is 20;
		var c = x is 0;
		var d = y is > 0;
		var e = obj is int;
		var g = x == 0 || x == 20 || x == 40 || x == 60 || x == 80;
		var f = ch is 'a' or 'e' or 'i' or 'o' or 'u';
		var h = x == 1 || x == 2 || x == 3 || x == 4 || x == 5;
		var i = x == 1 || x == 3 || x == 4 || x == 8 || x == 10;
		var j = x is <= 1 or > 3;

		return [ a, b, c, d, e, f, g, h, i, j ];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, obj, ch) => [ true, false, x == 0, y > 0, obj is int, (uint) (ch - 'a') <= 20U && (0x104111u >> ch - 'a' & 1) != 0, (uint) x <= 80U && x % 20 == 0, (uint) (x - 1) <= 4U, (uint) (x - 1) <= 9U && (0x28Du >> x - 1 & 1) != 0, (uint) (x - 2) > 1U ]),
		Create((_, _, _, _) => [ true, false, true, false, true, false, true, false, false, true ], [ 0, -5, 42, 'b' ]),
		Create((_, _, _, _) => [ true, false, false, true, true, false, false, false, true, true ], [ 10, 20, 100, 'c' ]),
		Create((_, _, _, _) => [ true, false, false, true, false, false, false, true, false, true ], [ 5, 15, "hello", 'd' ]),
		Create((_, _, _, _) => [ true, false, false, false, true, true, false, false, false, false ], [ -10, -20, 0, 'e' ])
	];
}