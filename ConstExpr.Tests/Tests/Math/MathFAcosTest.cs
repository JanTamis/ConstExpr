using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Acos(float) → FastAcos(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAcosTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Acos(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcos(x);"),
	];
}