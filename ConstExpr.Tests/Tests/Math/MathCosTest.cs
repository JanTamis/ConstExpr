using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Cos(double) → FastCos(x) in FastMath mode (polynomial approximation).</summary>
[InheritsTests]
public class MathCosTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Cos(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCos(x);"),
	];
}