using System.Numerics.Tensors;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Append() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqAppendOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Append() => collection.Append() (skip AsEnumerable)
		var a = x.AsEnumerable().Append(20).Sum();

		// ToList().Append() => collection.Append() (skip ToList)
		var b = x.ToList().Append(30).Sum();

		// ToArray().Append() => collection.Append() (skip ToArray)
		var c = x.ToArray().Append(40).Sum();

		// Multiple skip operations
		var d = x.AsEnumerable().ToList().Append(50).Sum();

		// Regular Append (should not be optimized)
		var e = x.Append(10).Sum();

		// Append followed by Count should be optimized to Length + number of appends
		var f = x.Append(20).Append(30).Append(40).Append(50).Append(10).Count();

		// concat followed by Count should be optimized to Length + number of concatenated elements
		var g = x.Concat([ 1, 2, 3, 4 ]).Count();

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => TensorPrimitives.Sum(x) * 5 + (x.Length << 1) + 159),
		Create(x => (x.Length << 1) + TensorPrimitives.Sum(x) + 183, [ new[] { 1, 2, 3 } ]),
		Create(x => (x.Length << 1) + TensorPrimitives.Sum(x) + 159, [ System.Array.Empty<int>() ]),
		Create(x => (x.Length << 1) + TensorPrimitives.Sum(x) + 199, [ new[] { 10 } ])
	];
}