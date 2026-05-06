using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.MaxMagnitude(float, float) — re-targets to float.MaxMagnitude.</summary>
[InheritsTests]
public class MathFMaxMagnitudeTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => System.MathF.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Single.MaxMagnitude(a, b);"),
		Create("return -3F;", 1.0f, -3.0f),
	];
}