using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Cbrt(double) → FastCbrt(x) in FastMath mode.</summary>
[InheritsTests]
public class MathCbrtTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Cbrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCbrt(x);"),
		Create(_ => 2D, [ 8.0 ]),
		Create(_ => 3D, [ 27.0 ])
	];
}