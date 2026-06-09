using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MinMagnitude(double, double) — re-targets to double.MinMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMinMagnitudeTest() : BaseTest<Func<double, double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => System.Math.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.MinMagnitude(a, b)),
		Create((_, _) => 1D, [ 1.0, -3.0 ]),
		Create((_, _) => -2D, [ -2.0, 5.0 ]),
	];
}