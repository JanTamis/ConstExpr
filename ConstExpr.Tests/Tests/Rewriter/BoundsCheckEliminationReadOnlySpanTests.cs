using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>ReadOnlySpan&lt;T&gt;</c> uses the same entry point as <c>Span&lt;T&gt;</c>; only reads
///   exist to rewrite.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationReadOnlySpanTests() : BaseTest<Func<ReadOnlySpan<int>, int, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString((data, i) => data[i] + data[0]);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((data, i) =>
		{
			ref var dataRef = ref MemoryMarshal.GetReference(data);

			return Unsafe.Add(ref dataRef, (nuint) i) + dataRef;
		}, [ Unknown, Unknown ])
	];
}