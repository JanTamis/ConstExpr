using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class FindMaxTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 50;", new[] { 10, 20, 50, 30 }),
		Create("return 100;", new[] { 5, 15, 25, 100, 50 }),
		Create("return -5;", new[] { -10, -20, -5, -30 }),
	];

	public override string TestMethod => """
		int FindMax(params int[] numbers)
		{
			if (numbers.Length == 0)
			{
				return 0;
			}

			var max = numbers[0];

			for (var i = 1; i < numbers.Length; i++)
			{
				if (numbers[i] > max)
				{
					max = numbers[i];
				}
			}

			return max;
		}
		""";
}
