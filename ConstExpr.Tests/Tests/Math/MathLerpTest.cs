namespace ConstExpr.Tests.Math;

/// <summary>Math.Lerp(double, double, double) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathLerpTest : BaseTestWithRandomValues<Func<double, double, double, double>>
{
	public override string TestMethod => GetString((a, b, t) => Double.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);"),
	];
}