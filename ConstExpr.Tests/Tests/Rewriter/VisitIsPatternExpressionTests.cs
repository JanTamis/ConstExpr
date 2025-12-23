namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitIsPatternExpression - constant comparison folding
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

		return [a, b, c, d, e, f, g, h, i];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var c = x == 0;
			var d = y > 0;
			var e = obj is int;
			var g = (uint)x <= 80U && x % 20 == 0;
			var f = ch - 'a' <= '\u0014' && (0x104111 >> ch - 'a' & 1) != 0;
			var h = (uint)(x - 1) <= 4U;
			var i = (uint)(x - 1) <= 9U && (0x28D >> x - 1 & 1) != 0;
			
			return [true, false, c, d, e, f, g, h, i];
			""", Unknown, Unknown, Unknown, Unknown),
		Create("return [true, false, true, false, true, false, true, false, false];", 0, -5, 42, 'b'),
		Create("return [true, false, false, true, true, false, false, false, true];", 10, 20, 100, 'c'),
		Create("return [true, false, false, true, false, false, false, true, false];", 5, 15, "hello", 'd'),
		Create("return [true, false, false, false, true, true, false, false, false];", -10, -20, 0, 'e')
	];
}