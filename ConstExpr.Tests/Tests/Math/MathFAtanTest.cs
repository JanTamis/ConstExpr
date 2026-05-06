using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Atan(float) → FastAtan(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAtanTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Atan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan(x);"),
	];
}