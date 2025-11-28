using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayProductTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 120;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 1;", System.Array.Empty<int>()),
		Create("return 0;", new[] { 5, 0, 3 }),
	];

	public override string TestMethod => """
		int ArrayProduct(int[] arr)
		{
			var product = 1;
			foreach (var num in arr)
			{
				product *= num;
			}
			return product;
		}
		""";
}

