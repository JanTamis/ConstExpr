using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Acos(double) → FastAcos(x) in FastMath mode.</summary>
[InheritsTests]
public class MathAcosTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Acos(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcos(x);")
	];
}