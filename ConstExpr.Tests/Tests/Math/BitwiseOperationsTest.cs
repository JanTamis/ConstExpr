namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 14;", 12, 10),
		Create("return 8;", 8, 8),
		Create("return 5;", 5, 0),
	];

	public override string TestMethod => """
		int BitwiseOr(int a, int b)
		{
			return (a & b) | (a ^ b);
		}
		""";
}


