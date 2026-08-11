namespace ConstExpr.Tests.Math;

/// <summary>MathF.MinMagnitude(float, float) — re-targets to float.MinMagnitude.</summary>
[InheritsTests]
public class MathFMinMagnitudeTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => MathF.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Single.MinMagnitude(a, b)),
	];
}