using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ElementAtOrDefault() optimization with Skip - verify that Skip is properly optimized
///   When Skip results in ElementAtOrDefault(0), it should further optimize to FirstOrDefault()
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultSkipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(1).ElementAtOrDefault(0) - should optimize to x.ElementAtOrDefault(1) (not FirstOrDefault because index after Skip is 1)
		var a = x.Skip(1).ElementAtOrDefault(0);

		// Skip with constant index - should optimize to x.ElementAtOrDefault(2 + 1) => x.ElementAtOrDefault(3)
		var b = x.Skip(2).ElementAtOrDefault(1);

		// Skip followed by AsEnumerable - should still optimize
		var c = x.Skip(1).AsEnumerable().ElementAtOrDefault(2);

		// Skip followed by ToArray, ElementAtOrDefault(0) - should optimize to x.ElementAtOrDefault(1)
		var d = x.Skip(1).ToArray().ElementAtOrDefault(0);

		// Multiple operations that don't affect indexing, then Skip
		var e = x.AsEnumerable().ToList().Skip(1).ElementAtOrDefault(1);

		// Skip with out of bounds index - should return default
		var f = x.Skip(1).ElementAtOrDefault(10);

		// Direct ElementAtOrDefault(0) without Skip - should become FirstOrDefault()
		var g = x.ElementAtOrDefault(0);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			ref var xRef = ref MemoryMarshal.GetArrayDataReference(x);

			return (x.Length > 1 ? Unsafe.Add(ref xRef, 1) << 1 : 0) + (x.Length > 3 ? Unsafe.Add(ref xRef, 3) << 1 : 0) + (x.Length > 2 ? Unsafe.Add(ref xRef, 2) : 0) + (x.Length > 11 ? Unsafe.Add(ref xRef, 11) : 0) + (x.Length > 0 ? xRef : 0);
		}),
		Create(_ => 16, [ new[] { 1, 2, 3, 4, 5 } ]), // 2 + 4 + 4 + 2 + 3 + 0 + 1 = 16
		Create(_ => 0, [ System.Array.Empty<int>() ]) // All return 0 (default)
	];
}