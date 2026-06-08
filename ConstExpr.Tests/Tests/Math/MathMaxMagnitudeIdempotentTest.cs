using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitude(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxMagnitudeIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(a => System.Math.MaxMagnitude(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a),
	];
}