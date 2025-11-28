namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MinOfTwoTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 5;", 5, 10),
		Create("return -10;", -10, 20),
		Create("return 0;", 0, 0),
	];

	public override string TestMethod => """
		int MinOfTwo(int a, int b)
		{
			return a < b ? a : b;
		}
		""";
}


