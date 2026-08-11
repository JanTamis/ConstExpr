namespace ConstExpr.Tests.Math;

/// <summary>MathF.MaxMagnitude(float, float) — re-targets to float.MaxMagnitude.</summary>
[InheritsTests]
public class MathFMaxMagnitudeTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => MathF.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Single.MaxMagnitude(a, b)),
	];
}