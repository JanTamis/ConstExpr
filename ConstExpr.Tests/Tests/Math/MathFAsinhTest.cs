namespace ConstExpr.Tests.Math;

/// <summary>MathF.Asinh(float) -> FastAsinh(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAsinhTest : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Asinh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAsinh(x);")
	];
}