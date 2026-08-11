using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ElementAt() optimization with Skip - verify that Skip is properly optimized
///   When Skip results in ElementAt(0), it should NOT further optimize to First() for arrays because
///   we already have direct indexing. Direct ElementAt(0) without Skip DOES become First().
/// </summary>
[InheritsTests]
public class LinqElementAtSkipOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(1).ElementAt(0) - should optimize to x[1] (not First because we have array access)
		var a = x.Skip(1).ElementAt(0);

		// Skip with constant index - should optimize to x[2 + 1] => x[3]
		var b = x.Skip(2).ElementAt(1);

		// Skip followed by AsEnumerable - should still optimize
		var c = x.Skip(1).AsEnumerable().ElementAt(2);

		// Skip(1).ToArray().ElementAt(0) - should optimize to x[1]
		var d = x.Skip(1).ToArray().ElementAt(0);

		// Multiple operations that don't affect indexing, then Skip
		var e = x.AsEnumerable().ToList().Skip(1).ElementAt(1);

		// Direct ElementAt(0) without Skip - should become First()
		var f = x.ElementAt(0);

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			ref var xRef = ref MemoryMarshal.GetArrayDataReference(x);

			return (Unsafe.Add(ref xRef, 1) << 1) + (Unsafe.Add(ref xRef, 3) << 1) + Unsafe.Add(ref xRef, 2) + xRef;
		}),
	];
}