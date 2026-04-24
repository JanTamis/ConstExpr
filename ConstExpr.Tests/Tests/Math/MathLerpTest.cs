using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Lerp(double, double, double) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathLerpTest() : BaseTest<Func<double, double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b, t) => double.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);"),
		Create("return 5D;", 0.0, 10.0, 0.5),
		Create("return 0D;", 0.0, 10.0, 0.0),
		Create("return 10D;", 0.0, 10.0, 1.0),
	];
}

/// <summary>MathF.Lerp(float, float, float) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathFLerpTest() : BaseTest<Func<float, float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b, t) => float.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);"),
	];
}



