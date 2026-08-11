namespace ConstExpr.Tests.Rewriter;

/// <summary>a / Sqrt(b) → a * ReciprocalSqrtEstimate(b).</summary>
[InheritsTests]
public class DivideBySqrtToReciprocalSqrtTest : BaseTestWithRandomValues<Func<double, double>>
{

	public override string TestMethod => GetString(x => 2d / Double.Sqrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ReciprocalSqrtEstimate(x) * 2d)
	];
}