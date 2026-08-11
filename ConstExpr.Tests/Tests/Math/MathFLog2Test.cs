namespace ConstExpr.Tests.Math;

/// <summary>MathF.Log2(float) -> FastLog2(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLog2Test : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Log2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog2(x);")
	];
}