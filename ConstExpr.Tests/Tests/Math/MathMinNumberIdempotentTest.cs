namespace ConstExpr.Tests.Math;

/// <summary>double.MinNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinNumberIdempotentTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(a => Double.MinNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}