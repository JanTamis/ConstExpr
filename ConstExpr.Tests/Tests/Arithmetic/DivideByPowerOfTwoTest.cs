using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DivideByPowerOfTwoTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((n, power) => n >> power);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 2;", 10, 2),
		Create("return 0;", 1, 5),
		Create("return 4;", 128, 5)
	];
}