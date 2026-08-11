using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The array-initializer merge also fires when the array is declared with an explicit type
///   (<c>int[] result = new int[N]</c>). The declared type is subsequently normalized to <c>var</c>
///   by the existing local-declaration simplification, since the initializer makes the type obvious.
/// </summary>
[InheritsTests]
public class ArrayElementInitializerExplicitTypeMergeTest : BaseTestWithRandomValues<Func<int[], int, int[]>>
{
	public override string TestMethod => GetString((numbers, positions) =>
	{
		var result = new int[3];

		result[0] = numbers[positions % 3];
		result[1] = numbers[(positions + 1) % 3];
		result[2] = numbers[(positions + 2) % 3];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, positions) =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);

			return
			[
				Unsafe.Add(ref numbersRef, positions % 3),
				Unsafe.Add(ref numbersRef, (positions + 1) % 3),
				Unsafe.Add(ref numbersRef, (positions + 2) % 3)
			];
		})
	];
}