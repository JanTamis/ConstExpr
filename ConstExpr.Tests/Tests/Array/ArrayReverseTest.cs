using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayReverseTest() : BaseTest<Func<int[], int[]>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
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
			var left = 0;
			var right = arr.Length - 1;

			while (left < right)
			{
				(arr[left], arr[right]) = (arr[right], arr[left]);

				left++;
				right--;
			}

			return arr;
		}),
		Create(_ => [ 5, 4, 3, 2, 1 ], [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => [ ], [ System.Array.Empty<int>() ]),
		Create(_ => [ 42 ], [ new[] { 42 } ])
	];
}