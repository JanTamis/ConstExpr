using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class BinarySearchTest() : BaseTest<Func<int[], int, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
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
			""", Unknown, Unknown),
		Create("return 2;", new[] { 1, 3, 5, 7, 9 }, 5),
		Create("return 4;", new[] { 0, 2, 4, 6, 8, 10 }, 8),
		Create("return -1;", new[] { 2, 4, 6, 8 }, 5)
	];
}