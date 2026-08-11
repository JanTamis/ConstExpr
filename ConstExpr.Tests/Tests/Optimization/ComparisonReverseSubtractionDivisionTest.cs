namespace ConstExpr.Tests.Optimization;

/// <summary>
///   c - v OP k isolates to v OP' c - k, always flipping the operator — the coefficient of v in
///   c - v is -1, so this is a different code path from the multiply case's negative-coefficient
///   flip even though the outcome (a flip) looks the same.
/// </summary>
[InheritsTests]
public class ComparisonReverseSubtractionDivisionTest : BaseTestWithRandomValues<Func<float, (bool, bool)>>
{
	public override string TestMethod => GetString(x => (3 - x < 1, 3 - x >= 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x > 2f, x <= 2f)),
	];
}