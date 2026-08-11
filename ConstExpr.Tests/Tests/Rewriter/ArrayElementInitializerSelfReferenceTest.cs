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