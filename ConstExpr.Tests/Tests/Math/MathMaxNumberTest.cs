namespace ConstExpr.Tests.Math;

/// <summary>double.MaxNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxNumberTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => Double.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(1.0, 2.0),
		CreateFolded(-5.0, 3.0)
	];
}