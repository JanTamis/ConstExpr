using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(a => double.MaxMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}