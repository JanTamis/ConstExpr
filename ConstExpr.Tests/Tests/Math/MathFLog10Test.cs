namespace ConstExpr.Tests.Math;

/// <summary>MathF.Log10(float) → FastLog10(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLog10Test : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Log10(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog10(x);")
	];
}