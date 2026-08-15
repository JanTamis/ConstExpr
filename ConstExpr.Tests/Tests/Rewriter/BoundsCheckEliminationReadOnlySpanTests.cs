using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The delegate takes <c>int[]</c>, not <c>ReadOnlySpan&lt;int&gt;</c>, directly: it is a ref struct
///   and cannot be boxed into the <c>object?[]</c> that
///   <see cref="BaseTestWithRandomValues{TDelegate}" />'s <c>Delegate.DynamicInvoke</c>-based fuzzing
///   requires (no implementation could box a ref struct - this is a CLR rule, not a gap to close).
///   <para>
///     <c>ReadOnlySpan&lt;int&gt; data = array;</c> is itself folded away by the interpreter before
///     <c>BoundsCheckRewriter</c> ever runs: it recognizes the conversion as a pure alias with no
///     slicing, so every read of <c>data</c> resolves straight through to <c>array</c>. What this class
///     ends up exercising is therefore the same array entry point
///     (<c>MemoryMarshal.GetArrayDataReference</c>) as the plain-array tests, not the
///     <c>ReadOnlySpan&lt;T&gt;</c>-specific <c>MemoryMarshal.GetReference</c> path — that path is
///     covered by <see cref="BoundsCheckEliminationStackAllocTests" />, whose <c>stackalloc</c> local
///     can't be aliased away the same way.
///   </para>
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationReadOnlySpanTests : BaseTestWithRandomValues<Func<int[], int, int>>
{
	// data[i] and data[0] need a non-empty array and an in-range i. A full-range random int never is, so every
	// draw threw and was discarded - the random pass checked nothing at all. Capped to 0-3 against the
	// generator's 0-8 element arrays.
	protected override int MaxRandomMagnitudeBits => 2;

	// Floor well under the count actually achieved, so a future generator or seed change that silently
	// starves this class again fails loudly instead of quietly checking one case.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString((array, i) =>
	{
		ReadOnlySpan<int> data = array;

		return data[i] + data[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((array, i) =>
		{
			ref var arrayRef = ref MemoryMarshal.GetArrayDataReference(array);

			return Unsafe.Add(ref arrayRef, i) + arrayRef;
		}, [ Unknown, Unknown ])
	];
}