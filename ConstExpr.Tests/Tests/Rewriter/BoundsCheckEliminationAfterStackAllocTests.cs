using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The two passes chained: <see cref="OptimizationFlags.StackAllocConversion" /> turns the heap
///   array into a stackalloc-backed span, and bounds-check elimination then takes a reference into
///   that span. Pins the hand-off, which only works because the <c>Span&lt;T&gt;</c> type survives.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationAfterStackAllocTests : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var counts = new int[8];

		counts[n % 8]++;

		return counts[n % 8] + counts[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			Span<int> counts = stackalloc int[8];
			ref var countsRef = ref MemoryMarshal.GetReference(counts);

			var mod = n % 8;

			Unsafe.Add(ref countsRef, mod)++;

			return Unsafe.Add(ref countsRef, mod) + countsRef;
		})
	];
}