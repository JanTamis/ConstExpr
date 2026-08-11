namespace ConstExpr.Tests.Math;

/// <summary>MathF.Atanh(float) -> FastAtanh(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAtanhTest : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Atanh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtanh(x);")
	];
}