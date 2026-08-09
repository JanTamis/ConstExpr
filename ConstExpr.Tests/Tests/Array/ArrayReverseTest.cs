using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayReverseTest : BaseTestWithRandomValues<Func<int[], int[]>>
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
	];
}