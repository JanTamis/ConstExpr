using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ElementAt() optimization on List - verify that ElementAt is optimized to list indexing
///   ElementAt(0) becomes First()
/// </summary>
[InheritsTests]
public class LinqElementAtOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAt(0) on List - should become First()
		var a = x.ElementAt(0);

		// AsEnumerable().ElementAt() => list indexing
		var b = x.AsEnumerable().ElementAt(1);

		// ToArray().ElementAt(0) => First()
		var c = x.ToArray().ElementAt(0);

		// ToList().ElementAt() => list indexing
		var d = x.ToList().ElementAt(1);

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			ref var xRef = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(x));

			return (xRef << 1) + (Unsafe.Add(ref xRef, 1) << 1);
		}),
		Create(_ => 6, [ new List<int> { 1, 2, 3, 4, 5 } ]), // 1 + 2 + 1 + 2 = 6
		Create(_ => 0, [ new List<int> { 0, 0, 0, 0, 0 } ])
	];
}