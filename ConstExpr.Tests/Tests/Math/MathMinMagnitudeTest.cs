using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MinMagnitude(double, double) — re-targets to double.MinMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMinMagnitudeTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => System.Math.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MinMagnitude(a, b);"),
		Create("return 1D;", 1.0, -3.0),
		Create("return -2D;", -2.0, 5.0),
	];
}

/// <summary>double.MinMagnitude(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => System.Math.MinMagnitude(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}

/// <summary>MathF.MinMagnitude(float, float) — re-targets to float.MinMagnitude.</summary>
[InheritsTests]
public class MathFMinMagnitudeTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => System.MathF.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.MinMagnitude(a, b);"),
		Create("return 1F;", 1.0f, -3.0f),
	];
}
