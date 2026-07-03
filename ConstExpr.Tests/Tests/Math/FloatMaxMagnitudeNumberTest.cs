using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>float.MaxMagnitudeNumber(a, b) — optimizer re-targets to float.MaxMagnitudeNumber.</summary>
[InheritsTests]
public class FloatMaxMagnitudeNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((a, b) => Single.MaxMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => -3F, [ 1.0f, -3.0f ])
	];
}