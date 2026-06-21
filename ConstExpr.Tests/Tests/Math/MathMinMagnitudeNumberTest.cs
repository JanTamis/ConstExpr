using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => Double.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 1D, [ 1.0, -3.0 ]),
		Create((_, _) => -2D, [ -2.0, 5.0 ])
	];
}