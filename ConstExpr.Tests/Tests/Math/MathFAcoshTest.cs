using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Acosh(float) -> FastAcosh(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAcoshTest() : BaseTest<Func<float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Acosh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcosh(x);")
	];
}