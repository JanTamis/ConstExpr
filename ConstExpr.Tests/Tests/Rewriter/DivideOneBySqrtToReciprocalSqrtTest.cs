namespace ConstExpr.Tests.Rewriter;

/// <summary>1 / Sqrt(b) → ReciprocalSqrtEstimate(b), without the redundant multiply.</summary>
[InheritsTests]
public class DivideOneBySqrtToReciprocalSqrtTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => 1d / Double.Sqrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ReciprocalSqrtEstimate(x))
	];
}