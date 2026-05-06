using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

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