using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class IsSortedTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", new[] { 1, 2, 3, 4, 5 }),
		Create("return false;", new[] { 5, 3, 1, 2 }),
		Create("return true;", new[] { 10, 20, 30 }),
	];

	public override string TestMethod => """
		bool IsSorted(params int[] numbers)
		{
			if (numbers.Length <= 1)
			{
				return true;
			}

			for (var i = 1; i < numbers.Length; i++)
			{
				if (numbers[i] < numbers[i - 1])
				{
					return false;
				}
			}

			return true;
		}
		""";
}
