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
		var k = new[] { 99 }.Concat(x).Sum();

		// Single element Concat with array syntax => Append
		var l = x.Concat(new[] { 88 }).Sum();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Sum_7Ump6A(x);
			var b = Sum_7Ump6A(x);
			var c = Sum_7Ump6A(x) + 30;
			var d = Sum_7Ump6A(x) + 5;
			var e = Sum_7Ump6A(x) + 15;
			var f = Sum_7Ump6A(x) + 25;
			var g = Sum_7Ump6A(x) + 70;
			var h = Sum_7Ump6A(x) + 10;
			var i = Sum_7Ump6A(x) + 300;
			var j = Sum_7Ump6A(x) + 60;
			var k = Sum_7Ump6A(x) + 99;
			var l = Sum_7Ump6A(x) + 88;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 774;", new[] { 1, 2, 3 }),
		Create("return 702;", new int[] { }),
		Create("return 882;", new[] { 5, 10 }),
	];
}
