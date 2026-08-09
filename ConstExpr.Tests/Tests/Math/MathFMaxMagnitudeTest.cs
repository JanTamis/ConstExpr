namespace ConstExpr.Tests.Math;

/// <summary>MathF.MaxMagnitude(float, float) — re-targets to float.MaxMagnitude.</summary>
[InheritsTests]
public class MathFMaxMagnitudeTest : BaseTest<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => MathF.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Single.MaxMagnitude(a, b)),
		CreateFolded(1.0f, -3.0f)
	];
}