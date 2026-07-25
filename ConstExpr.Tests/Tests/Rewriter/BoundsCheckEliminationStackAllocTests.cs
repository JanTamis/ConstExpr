using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A stackalloc-backed local keeps its declared <c>Span&lt;T&gt;</c> type through the pipeline —
///   erasing it to <c>var</c> would turn it into an <c>int*</c> — so it is recognised as a span here.
///   This is also the hand-off from <see cref="OptimizationFlags.StackAllocConversion" />, which
///   produces exactly this shape.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationStackAllocTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		Span<int> buf = stackalloc int[4];

		buf[n % 4] = n;

		return buf[n % 4] + buf[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			Span<int> buf = stackalloc int[4];
			ref var bufRef = ref MemoryMarshal.GetReference(buf);
			var mod = n % 4;

			Unsafe.Add(ref bufRef, mod) = n;

			return Unsafe.Add(ref bufRef, mod) + bufRef;
		}, [ Unknown ])
	];
}