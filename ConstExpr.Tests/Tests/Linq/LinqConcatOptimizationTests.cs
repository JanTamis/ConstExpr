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
			var c = x.Concat(new[] { 10, 20 }).Sum();
			var d = x.Append(5).Sum();
			var e = x.Append(15).Sum();
			var f = x.Append(25).Sum();
			var g = x.Concat(new[] { 30, 40 }).Sum();
			var h = x.Concat([1, 2, 3, 4]).Sum();
			var i = x.Append(100).Append(200).Sum();
			var j = x.Append(10).Append(20).Append(30).Sum();
			var k = x.Append(99).Sum();
			var l = x.Append(88).Sum();
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 762;", new[] { 1, 2, 3 }),
		Create("return 774;", new[] { 1, 2, 3 }),
		Create("return 702;", new int[] { }),
		Create("return 702;", new int[] { }),
		Create("return 852;", new[] { 5, 10 }),
		Create("return 882;", new[] { 5, 10 }),
	];
}
