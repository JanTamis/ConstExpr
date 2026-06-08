using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MaxMagnitude(double, double) — re-targets to double.MaxMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMaxMagnitudeTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => System.Math.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.MaxMagnitude(a, b)),
		Create((_, _) => -3D, [ 1.0, -3.0 ]),
		Create((_, _) => 5D, [ -2.0, 5.0 ]),
	];
}