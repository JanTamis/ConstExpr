namespace ConstExpr.Tests.Math;

/// <summary>MathF.Exp2(float) → FastExp2(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFExp2Test : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => Single.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);")
	];
}