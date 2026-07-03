using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.MaxMagnitude(float, float) — re-targets to float.MaxMagnitude.</summary>
[InheritsTests]
public class MathFMaxMagnitudeTest() : BaseTest<Func<float, float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((a, b) => MathF.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Single.MaxMagnitude(a, b)),
		Create((_, _) => -3F, [ 1.0f, -3.0f ])
	];
}