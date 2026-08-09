namespace ConstExpr.Tests.Math;

/// <summary>double.MinMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinMagnitudeNumberTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => Double.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(1.0, -3.0),
		CreateFolded(-2.0, 5.0)
	];
}