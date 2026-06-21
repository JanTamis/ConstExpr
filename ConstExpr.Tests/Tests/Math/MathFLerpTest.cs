using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Lerp(float, float, float) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathFLerpTest() : BaseTest<Func<float, float, float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b, t) => Single.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);")
	];
}