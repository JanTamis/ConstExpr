using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathAtan2Test() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((y, x) => System.Math.Atan2(y, x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan2(y, x);"),
		Create("return 0D;", 0.0, 2.0),
	];
}

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

