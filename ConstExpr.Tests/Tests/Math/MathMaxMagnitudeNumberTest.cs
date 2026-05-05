using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => double.MaxMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MaxMagnitudeNumber(a, b);"),
		Create("return -3D;", 1.0, -3.0),
		Create("return 5D;", -2.0, 5.0),
	];
}

/// <summary>double.MaxMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => double.MaxMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}

/// <summary>float.MaxMagnitudeNumber(a, b) — optimizer re-targets to float.MaxMagnitudeNumber.</summary>
[InheritsTests]
public class FloatMaxMagnitudeNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => float.MaxMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.MaxMagnitudeNumber(a, b);"),
		Create("return -3F;", 1.0f, -3.0f),
	];
}
