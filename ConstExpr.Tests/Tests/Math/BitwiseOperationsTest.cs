using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) => a & b | a ^ b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return a & b | a ^ b;", Unknown, Unknown),
		Create("return 14;", 12, 10),
		Create("return 8;", 8, 8),
		Create("return 5;", 5, 0)
	];
}