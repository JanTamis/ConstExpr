using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The merge bails when an assigned value reads the array that is still being built. An array
///   initializer cannot reference the variable it initializes, so the original element-by-element
///   form must be preserved.
/// </summary>
[InheritsTests]
public class ArrayElementInitializerSelfReferenceTest : BaseTestWithRandomValues<Func<int[], int[]>>
{
	// numbers[result[0]] indexes by an element value, so both numbers[0] and that value have to be in range.
	// With full-range random elements neither ever was, so every draw threw and was discarded - the random
	// pass checked nothing at all. Capped to 0-3 against the generator's 0-8 element arrays.
	protected override int MaxRandomMagnitudeBits => 2;

	// Floor well under the count actually achieved, so a future generator or seed change that silently
	// starves this class again fails loudly instead of quietly checking one case.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString(numbers =>
	{
		var result = new int[2];

		result[0] = numbers[0];
		result[1] = numbers[result[0]];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			var result = new int[2];

			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);
			resultRef = numbersRef;
			Unsafe.Add(ref resultRef, 1) = Unsafe.Add(ref numbersRef, resultRef);

			return result;
		})
	];
}