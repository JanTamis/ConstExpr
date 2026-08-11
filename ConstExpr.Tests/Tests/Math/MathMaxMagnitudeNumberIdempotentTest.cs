namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitudeNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberIdempotentTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(a => Double.MaxMagnitudeNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}