using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Asin(float) → FastAsin(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAsinTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Asin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAsin(x);"),
	];
}