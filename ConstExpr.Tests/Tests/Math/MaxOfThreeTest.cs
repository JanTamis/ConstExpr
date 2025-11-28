namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return 10;", 5, 10, 3),
		Create("return 5;", 5, 5, 5),
	];

	public override string TestMethod => """
		int MaxOfThree(int a, int b, int c)
		{
			var max = a;

			if (b > max)
			{
				max = b;
			}

			if (c > max)
			{
				max = c;
			}

			return max;
		}
		""";
}

