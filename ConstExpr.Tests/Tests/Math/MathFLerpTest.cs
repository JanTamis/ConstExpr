namespace ConstExpr.Tests.Math;

/// <summary>MathF.Lerp(float, float, float) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathFLerpTest : BaseTestWithRandomValues<Func<float, float, float, float>>
{
	public override string TestMethod => GetString((a, b, t) => Single.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);")
	];
}