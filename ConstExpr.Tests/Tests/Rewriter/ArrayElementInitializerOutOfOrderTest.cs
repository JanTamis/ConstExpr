using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The merge requires every index <c>0..N-1</c> to be assigned exactly once, in ascending order.
///   When the indices are written out of order the body is left untouched, because reordering the
///   values into an initializer could change evaluation order.
/// </summary>
[InheritsTests]
public class ArrayElementInitializerOutOfOrderTest : BaseTest<Func<int[], int[]>>
{
	public override string TestMethod => GetString(numbers =>
	{
		return new[]
		{
			numbers[0],
			numbers[1],
			numbers[2]
		};
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);

			return
			[
				numbersRef,
				Unsafe.Add(ref numbersRef, 1),
				Unsafe.Add(ref numbersRef, 2)
			];
		})
	];
}