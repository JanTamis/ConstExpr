using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SquareTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n => n * n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 25;", 5),
		Create("return 0;", 0),
		Create("return 100;", -10)
	];
}