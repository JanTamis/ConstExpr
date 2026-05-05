using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => double.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MinMagnitudeNumber(a, b);"),
		Create("return 1D;", 1.0, -3.0),
		Create("return -2D;", -2.0, 5.0),
	];
}

/// <summary>double.MinMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => double.MinMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}

/// <summary>float.MinMagnitudeNumber(a, b) — optimizer re-targets to float.MinMagnitudeNumber.</summary>
[InheritsTests]
public class FloatMinMagnitudeNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => float.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.MinMagnitudeNumber(a, b);"),
		Create("return 1F;", 1.0f, -3.0f),
	];
}
