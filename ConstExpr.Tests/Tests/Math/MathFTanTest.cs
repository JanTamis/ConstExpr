namespace ConstExpr.Tests.Math;

/// <summary>MathF.Tan(float) → FastTan(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFTanTest : BaseTest<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);")
	];
}