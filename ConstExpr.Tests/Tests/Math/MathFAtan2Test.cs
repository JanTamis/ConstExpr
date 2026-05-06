using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFAtan2Test() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((y, x) => System.MathF.Atan2(y, x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan2(y, x);"),
		Create("return 0F;", 0f, 2f),
	];
}