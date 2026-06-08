using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Atanh(float) -> FastAtanh(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAtanhTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Atanh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtanh(x);"),
	];
}