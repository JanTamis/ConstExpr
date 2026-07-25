namespace ConstExpr.Tests.Math;

/// <summary>MathF.Sin(float) → FastSin(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFSinTest : BaseTest<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Sin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastSin(x);")
	];
}