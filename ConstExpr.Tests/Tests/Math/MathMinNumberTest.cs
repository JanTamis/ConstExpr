namespace ConstExpr.Tests.Math;

/// <summary>double.MinNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinNumberTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => Double.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(1.0, 2.0),
		CreateFolded(-5.0, 3.0)
	];
}