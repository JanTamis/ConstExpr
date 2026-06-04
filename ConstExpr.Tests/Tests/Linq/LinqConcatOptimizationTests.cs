using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Concat() optimization - verify that Empty enumerables and unnecessary operations are optimized
/// </summary>
[InheritsTests]
public class LinqConcatOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Concat with Empty on right => collection (skip empty)
		var a = x.Concat([ ]).Sum();

		// Empty.Concat(collection) => collection (skip empty source)
		var b = Enumerable.Empty<int>().Concat(x).Sum();

		// AsEnumerable().Concat() => collection.Concat() (skip AsEnumerable)
		var c = x.AsEnumerable().Concat([ 10, 20 ]).Sum();

		// ToList().Concat() => collection.Concat() (skip ToList)
		var d = x.ToList().Concat([ 5 ]).Sum();

		// ToArray().Concat() => collection.Concat() (skip ToArray)
		var e = x.ToArray().Concat([ 15 ]).Sum();

		// Multiple skip operations
		var f = x.AsEnumerable().ToList().Concat([ 25 ]).Sum();

		// Regular Concat (should not be optimized further)
		var g = x.Concat([ 30, 40 ]).Sum();

		// Merge multiple Concat with collection literals: [1,2].Concat([3,4]) => [1,2,3,4]
		var h = x.Concat([ 1, 2 ]).Concat([ 3, 4 ]).Sum();

		// Merge multiple Concat with collection expressions (if supported)
		var i = x.Concat([ 100 ]).Concat([ 200 ]).Sum();

		// Merge chain of 3+ Concat operations
		var j = x.Concat([ 10 ]).Concat([ 20 ]).Concat([ 30 ]).Sum();

		// Single element Concat => Append
		var k = new[] { 99 }.Concat(x).Sum();

		// Single element Concat with array syntax => Append
		var l = x.Concat([ 88 ]).Sum();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return TensorPrimitives.Sum(x) * 12 + 702;"),
		Create(_ => 774, [ new[] { 1, 2, 3 } ]),
		Create(_ => 702, [ new int[] { } ]),
		Create(_ => 882, [ new[] { 5, 10 } ]),
	];
}