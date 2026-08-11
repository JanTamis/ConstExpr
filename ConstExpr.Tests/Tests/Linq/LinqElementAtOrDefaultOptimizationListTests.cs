using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ElementAtOrDefault() optimization on List
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultOptimizationListTests : BaseTestWithRandomValues<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAtOrDefault on List
		var a = x.ElementAtOrDefault(0);

		// AsEnumerable().ElementAtOrDefault() => ElementAtOrDefault()
		var b = x.AsEnumerable().ElementAtOrDefault(1);

		// ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var c = x.ToArray().ElementAtOrDefault(0);

		// ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var d = x.ToList().ElementAtOrDefault(1);

		// Out of bounds
		var e = x.ElementAtOrDefault(10);

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			ref var xRef = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(x));

			var xCount = x.Count;

			return (xCount > 0 ? xRef << 1 : 0) + (xCount > 1 ? Unsafe.Add(ref xRef, 1) << 1 : 0) + (xCount > 10 ? Unsafe.Add(ref xRef, 10) : 0);
		}),
	];
}