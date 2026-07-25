using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for stackalloc conversion. A local heap array of an unmanaged primitive, small constant
///   size, that never escapes and is only used in span-safe ways, becomes a <c>Span&lt;T&gt;</c>
///   backed by <c>stackalloc</c>. Runtime-indexed writes keep the array a sized <c>new int[8]</c>
///   (it cannot fold to a collection expression), so the sized-form conversion path is exercised.
/// </summary>
[InheritsTests]
public class StackAllocConversionTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var counts = new int[8];

		counts[n % 8]++;
		counts[(n + 1) % 8]++;

		return counts[n % 8] + counts[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown input: the array is a local, unmanaged, small and constant-sized, used only via
		// indexing — so it is converted to a stackalloc-backed Span<int>.
		Create(n =>
		{
			Span<int> counts = stackalloc int[8];
			ref var countsRef = ref MemoryMarshal.GetReference(counts);
			var mod = n % 8;

			Unsafe.Add(ref countsRef, mod)++;
			Unsafe.Add(ref countsRef, (n + 1) % 8)++;

			return Unsafe.Add(ref countsRef, mod) + countsRef;
		}, [ Unknown ]),

		// Known input folds through the interpreter: counts[3]=1, counts[4]=1, counts[3]+counts[0]=1.
		Create(_ => 1, [ 3 ])
	];
}