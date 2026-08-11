using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard test: an array whose stack footprint exceeds the byte cap (512 ints = 2048 B > 1024 B)
///   must be left on the heap — converting it would risk a stack overflow.
/// </summary>
[InheritsTests]
public class StackAllocConversionSkipsLargeArrayTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var big = new int[512];

		big[n % 512]++;

		return big[n % 512];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Too large for the stack: the declaration is left as a heap array, unchanged.
		Create(n =>
		{
			var big = new int[512];
			ref var bigRef = ref MemoryMarshal.GetArrayDataReference(big);
			var mod = n % 512;

			Unsafe.Add(ref bigRef, mod)++;

			return Unsafe.Add(ref bigRef, mod);
		})
	];
}