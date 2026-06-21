using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(a => Double.MinMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}