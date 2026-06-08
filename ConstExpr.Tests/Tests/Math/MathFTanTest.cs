using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Tan(float) → FastTan(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFTanTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);"),
	];
}