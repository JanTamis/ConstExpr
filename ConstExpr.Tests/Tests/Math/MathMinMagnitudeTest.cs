namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MinMagnitude(double, double) — re-targets to double.MinMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMinMagnitudeTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => System.Math.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.MinMagnitude(a, b)),
	];
}