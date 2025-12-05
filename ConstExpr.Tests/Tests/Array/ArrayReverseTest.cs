using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayReverseTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return [5, 4, 3, 2, 1];", new[] { 1, 2, 3, 4, 5 }),
		Create("return [];", System.Array.Empty<int>()),
		Create("return [42];", new[] { 42 }),
	];

	public override string TestMethod => """
		int[] ArrayReverse(int[] arr)
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
		}
		""";
}

