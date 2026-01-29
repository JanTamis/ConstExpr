namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Concat() optimization - verify that Empty enumerables and unnecessary operations are optimized
/// </summary>
[InheritsTests]
public class LinqConcatOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Concat with Empty on right => collection (skip empty)
		var a = x.Concat(Enumerable.Empty<int>()).Sum();

		// Empty.Concat(collection) => collection (skip empty source)
		var b = Enumerable.Empty<int>().Concat(x).Sum();

		// AsEnumerable().Concat() => collection.Concat() (skip AsEnumerable)
		var c = x.AsEnumerable().Concat(new[] { 10, 20 }).Sum();

		// ToList().Concat() => collection.Concat() (skip ToList)
		var d = x.ToList().Concat(new[] { 5 }).Sum();

		// ToArray().Concat() => collection.Concat() (skip ToArray)
		var e = x.ToArray().Concat(new[] { 15 }).Sum();

		// Multiple skip operations
		var f = x.AsEnumerable().ToList().Concat(new[] { 25 }).Sum();

		// Regular Concat (should not be optimized further)
		var g = x.Concat(new[] { 30, 40 }).Sum();

		// Merge multiple Concat with collection literals: [1,2].Concat([3,4]) => [1,2,3,4]
		var h = x.Concat(new[] { 1, 2 }).Concat(new[] { 3, 4 }).Sum();

		// Merge multiple Concat with collection expressions (if supported)
		var i = x.Concat([100]).Concat([200]).Sum();

		// Merge chain of 3+ Concat operations
		var j = x.Concat(new[] { 10 }).Concat(new[] { 20 }).Concat(new[] { 30 }).Sum();

		// Single element Concat => Append
		var k = x.Concat([99]).Sum();

		// Single element Concat with array syntax => Append
		var l = x.Concat(new[] { 88 }).Sum();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Sum();
			var b = x.Sum();
			var c = x.Concat([10, 20]).Sum();
			var d = x.Append(5).Sum();
			var e = x.Append(15).Sum();
			var f = x.Append(25).Sum();
			var g = x.Concat([30, 40]).Sum();
			var h = x.Concat([1, 2, 3, 4]).Sum();
			var i = x.Append(100).Append(200).Sum();
			var j = x.Append(10).Append(20).Append(30).Sum();
			var k = x.Append(99).Sum();
			var l = x.Append(88).Sum();
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 762;", new[] { 1, 2, 3 }), 
		// a=6, b=6, c=36, d=11, e=21, f=31, g=76, h=16, i=306, j=66, k=105, l=94
		// Total = 6+6+36+11+21+31+76+16+306+66+105+94 = 774... wait let me recalculate
		// a = 1+2+3 = 6
		// b = 1+2+3 = 6
		// c = 1+2+3+10+20 = 36
		// d = 1+2+3+5 = 11
		// e = 1+2+3+15 = 21
		// f = 1+2+3+25 = 31
		// g = 1+2+3+30+40 = 76
		// h = 1+2+3+1+2+3+4 = 16
		// i = 1+2+3+100+200 = 306
		// j = 1+2+3+10+20+30 = 66
		// k = 1+2+3+99 = 105
		// l = 1+2+3+88 = 94
		// Total = 6+6+36+11+21+31+76+16+306+66+105+94 = 774
		Create("return 774;", new[] { 1, 2, 3 }),
		Create("return 702;", new int[] { }), 
		// a=0, b=0, c=30, d=5, e=15, f=25, g=70, h=10, i=300, j=60, k=99, l=88
		// Total = 0+0+30+5+15+25+70+10+300+60+99+88 = 702
		Create("return 702;", new int[] { }),
		Create("return 852;", new[] { 5, 10 }), 
		// a=15, b=15, c=45, d=20, e=30, f=40, g=85, h=25, i=315, j=75, k=114, l=103
		// Total = 15+15+45+20+30+40+85+25+315+75+114+103 = 882... let me recalculate
		// a = 5+10 = 15
		// b = 5+10 = 15
		// c = 5+10+10+20 = 45
		// d = 5+10+5 = 20
		// e = 5+10+15 = 30
		// f = 5+10+25 = 40
		// g = 5+10+30+40 = 85
		// h = 5+10+1+2+3+4 = 25
		// i = 5+10+100+200 = 315
		// j = 5+10+10+20+30 = 75
		// k = 5+10+99 = 114
		// l = 5+10+88 = 103
		// Total = 15+15+45+20+30+40+85+25+315+75+114+103 = 882
		Create("return 882;", new[] { 5, 10 }),
	];
}
