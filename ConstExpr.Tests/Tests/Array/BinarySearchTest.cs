using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class BinarySearchTest : BaseTest<Func<int[], int, int>>
{
	public override string TestMethod => GetString((arr, target) =>
	{
		var left = 0;
		var right = arr.Length - 1;

		while (left <= right)
		{
			var mid = left + (right - left >> 1);
			var current = arr[mid];

			if (current == target)
			{
				return mid;
			}

			if (current < target)
			{
				left = mid + 1;
			}
			else
			{
				right = mid - 1;
			}
		}

		return -1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((arr, target) =>
		{
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			var left = 0;
			var right = arr.Length - 1;

			while (left <= right)
			{
				var mid = left + (right - left >> 1);
				var current = Unsafe.Add(ref arrRef, mid);

				if (current == target)
					return mid;

				if (current < target)
				{
					left = mid + 1;
				}
				else
				{
					right = mid - 1;
				}
			}

			return -1;
		}),
		CreateFolded(new[] { 1, 3, 5, 7, 9 }, 5),
		CreateFolded(new[] { 0, 2, 4, 6, 8, 10 }, 8),
		CreateFolded(new[] { 2, 4, 6, 8 }, 5)
	];
}