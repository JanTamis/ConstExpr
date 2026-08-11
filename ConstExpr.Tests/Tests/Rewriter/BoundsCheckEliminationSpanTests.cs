using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The delegate takes <c>int[]</c>, not <c>Span&lt;int&gt;</c>, directly: <c>Span&lt;T&gt;</c> is a
///   ref struct and cannot be boxed into the <c>object?[]</c> that
///   <see cref="BaseTestWithRandomValues{TDelegate}" />'s <c>Delegate.DynamicInvoke</c>-based fuzzing
///   requires (no implementation could box a ref struct - this is a CLR rule, not a gap to close).
///   <para>
///     <c>Span&lt;int&gt; buf = array;</c> is itself folded away by the interpreter before
///     <c>BoundsCheckRewriter</c> ever runs: it recognizes the conversion as a pure alias with no
///     slicing, so every read and write of <c>buf</c> resolves straight through to <c>array</c>. What
///     this class ends up exercising is therefore the same array entry point
///     (<c>MemoryMarshal.GetArrayDataReference</c>) as the plain-array tests, not the
///     <c>Span&lt;T&gt;</c>-specific <c>MemoryMarshal.GetReference</c> path — that path is covered by
///     <see cref="BoundsCheckEliminationStackAllocTests" />, whose <c>stackalloc</c> local can't be
///     aliased away the same way.
///   </para>
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationSpanTests : BaseTestWithRandomValues<Func<int[], int, int>>
{
	public override string TestMethod => GetString((array, i) =>
	{
		Span<int> buf = array;
		buf[i] = i;

		return buf[i] + buf[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((array, i) =>
		{
			ref var arrayRef = ref MemoryMarshal.GetArrayDataReference(array);

			Unsafe.Add(ref arrayRef, i) = i;

			return Unsafe.Add(ref arrayRef, i) + arrayRef;
		}, [ Unknown, Unknown ])
	];
}