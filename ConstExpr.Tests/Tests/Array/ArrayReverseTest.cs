using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayReverseTest() : BaseTest<Func<int[], int[]>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
		Create(null),
		Create(_ => [ 5, 4, 3, 2, 1 ], [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => [ ], [ System.Array.Empty<int>() ]),
		Create(_ => [ 42 ], [ new[] { 42 } ])
	];
}