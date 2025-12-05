using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ContainsElementTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", new[] { 1, 2, 3, 4, 5 }, 3),
		Create("return false;", new[] { 10, 20, 30 }, 5),
	];

	public override string TestMethod => """
		bool ContainsElement(int[] arr, int value)
		{
			foreach (var item in arr)
			{
				if (item == value)
				{
					return true;
				}
			}

			return false;
		}
		""";
}

