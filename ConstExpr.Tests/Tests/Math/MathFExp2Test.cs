using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Exp2(float) → FastExp2(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFExp2Test() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => float.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);"),
	];
}