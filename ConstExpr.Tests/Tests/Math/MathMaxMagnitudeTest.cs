using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MaxMagnitude(double, double) — re-targets to double.MaxMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMaxMagnitudeTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => System.Math.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MaxMagnitude(a, b);"),
		Create("return -3D;", 1.0, -3.0),
		Create("return 5D;", -2.0, 5.0),
	];
}