namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitude(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxMagnitudeIdempotentTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(a => System.Math.MaxMagnitude(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}