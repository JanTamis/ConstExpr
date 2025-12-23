using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MinOfTwoTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) => a < b ? a : b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return Int32.Min(a, b);", Unknown, Unknown),
		Create("return 5;", 5, 10),
		Create("return -10;", -10, 20),
		Create("return 0;", 0, 0)
	];
}