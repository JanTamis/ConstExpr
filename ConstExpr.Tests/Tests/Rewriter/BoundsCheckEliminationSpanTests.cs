using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>Span&lt;T&gt;</c> goes through <c>MemoryMarshal.GetReference</c> instead of
///   <c>GetArrayDataReference</c>. Being invariant, it has no array-store covariance check to lose,
///   so writes are rewritten for any element type.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationSpanTests() : BaseTest<Func<Span<int>, int, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString((buf, i) =>
	{
		buf[i] = i;

		return buf[i] + buf[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((buf, i) =>
		{
			ref var bufRef = ref MemoryMarshal.GetReference(buf);

			Unsafe.Add(ref bufRef, (nuint) i) = i;

			return Unsafe.Add(ref bufRef, (nuint) i) + bufRef;
		}, [ Unknown, Unknown ])
	];
}