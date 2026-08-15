using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Optimization;

// Regression: `arr[k]` is read, then `arr` is written via an indexer, then `arr[k]` is read again.
// The two reads are NOT the same value, so CSE must not merge them. A buggy pass tracks only plain
// identifier assignments as mutations and misses the indexer write, hoisting a single `arr[k]`.
[InheritsTests]
public class CommonSubexpressionElementAccessMutationTest : BaseTestWithRandomValues<Func<int[], int, int>>
{
	// arr[k] and arr[0] need a non-empty array and an in-range k. A full-range random int never is, so every
	// draw threw and was discarded - the random pass checked nothing at all. Capped to 0-3 against the
	// generator's 0-8 element arrays.
	protected override int MaxRandomMagnitudeBits => 2;

	// Floor well under the count actually achieved, so a future generator or seed change that silently
	// starves this class again fails loudly instead of quietly checking one case.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString((arr, k) =>
	{
		var x = arr[k];
		arr[0] = 9;
		var y = arr[k];
		return x + x + y + y;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((arr, k) =>
		{
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			var x = Unsafe.Add(ref arrRef, k);
			arrRef = 9;

			var y = Unsafe.Add(ref arrRef, k);

			return (x << 1) + (y << 1);
		})
	];
}