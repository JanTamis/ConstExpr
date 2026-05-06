using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => double.MinMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}