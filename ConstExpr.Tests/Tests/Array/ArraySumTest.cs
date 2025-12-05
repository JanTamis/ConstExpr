using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArraySumTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 15;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", System.Array.Empty<int>()),
		Create("return 42;", new[] { 42 }),
	];

	public override string TestMethod => """
		int ArraySum(int[] arr)
		{
			var sum = 0;
			foreach (var num in arr)
			{
				sum += num;
			}
			return sum;
		}
		""";
}

