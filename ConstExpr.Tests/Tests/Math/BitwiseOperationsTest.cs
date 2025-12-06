using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return a & b | a ^ b;", Unknown, Unknown),
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