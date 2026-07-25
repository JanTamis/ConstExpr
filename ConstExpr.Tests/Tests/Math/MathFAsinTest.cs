namespace ConstExpr.Tests.Math;

/// <summary>MathF.Asin(float) → FastAsin(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAsinTest : BaseTest<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Asin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAsin(x);")
	];
}