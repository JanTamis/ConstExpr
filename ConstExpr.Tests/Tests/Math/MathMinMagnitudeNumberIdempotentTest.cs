namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberIdempotentTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(a => Double.MinMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}