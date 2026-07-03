using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Asin(float) → FastAsin(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAsinTest() : BaseTest<Func<float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(x => MathF.Asin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAsin(x);")
	];
}