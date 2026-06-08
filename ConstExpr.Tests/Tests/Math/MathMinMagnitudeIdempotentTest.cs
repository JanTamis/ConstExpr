using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitude(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(a => System.Math.MinMagnitude(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a),
	];
}