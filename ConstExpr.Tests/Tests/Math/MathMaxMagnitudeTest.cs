namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MaxMagnitude(double, double) — re-targets to double.MaxMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMaxMagnitudeTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => System.Math.MaxMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.MaxMagnitude(a, b)),
	];
}