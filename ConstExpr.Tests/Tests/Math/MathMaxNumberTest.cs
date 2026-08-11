namespace ConstExpr.Tests.Math;

/// <summary>double.MaxNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxNumberTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((a, b) => Double.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}