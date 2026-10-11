using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A write through <c>Unsafe.Add</c> skips the array-store covariance check, so for a reference
///   element type only reads may be rewritten. This is the one guard whose failure would be silent
///   — no compile error, just a type hole — so it is pinned here.
/// </summary>
// Not a random-value test: the method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.
[InheritsTests]
public class BoundsCheckEliminationReferenceElementTests : BaseTest<Func<string[], int>>
{
	public override string TestMethod => GetString(words =>
	{
		words[0] = "x";

		return words[1].Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(words =>
		{
			ref var wordsRef = ref MemoryMarshal.GetArrayDataReference(words);

			words[0] = "x";

			return Unsafe.Add(ref wordsRef, 1).Length;
		}),
		Create("""
			words[0] = "x";

			return 2;
			""", (object) new[] { "a", "bc" })
	];
}