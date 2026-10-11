using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Array;

// Not a random-value test: the method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.
[InheritsTests]
public class ArrayReverseTest : BaseTest<Func<int[], int[]>>
{
	public override string TestMethod => GetString(arr =>
	{
		var left = 0;
		var right = arr.Length - 1;

		while (left < right)
		{
			var temp = arr[left];
			arr[left] = arr[right];
			arr[right] = temp;

			left++;
			right--;
		}

		return arr;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(arr =>
		{
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);

			var left = 0;
			var right = arr.Length - 1;

			while (left < right)
			{
				(Unsafe.Add(ref arrRef, left), Unsafe.Add(ref arrRef, right)) = (Unsafe.Add(ref arrRef, right), Unsafe.Add(ref arrRef, left));

				left++;
				right--;
			}

			return arr;
		}),
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			arrRef = 5;
			Unsafe.Add(ref arrRef, 4) = 1;
			Unsafe.Add(ref arrRef, 1) = 4;
			Unsafe.Add(ref arrRef, 3) = 2;

			return arr;
			""", new[] { 1, 2, 3, 4, 5 }),
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			arrRef = 4;
			Unsafe.Add(ref arrRef, 3) = 1;
			Unsafe.Add(ref arrRef, 1) = 3;
			Unsafe.Add(ref arrRef, 2) = 2;

			return arr;
			""", new[] { 1, 2, 3, 4 })
	];
}