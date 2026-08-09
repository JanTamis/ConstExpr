namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => Double.MaxMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(1.0, -3.0),
		CreateFolded(-2.0, 5.0)
	];
}