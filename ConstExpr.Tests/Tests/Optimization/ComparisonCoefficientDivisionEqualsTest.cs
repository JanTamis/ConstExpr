namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v * c == k and v * c != k isolate to v == k / c and v != k / c. Equality has no direction to
///   flip, so both are just the coefficient-division transform with the same operator kind on both
///   sides of the strategy's flip decision.
/// </summary>
[InheritsTests]
public class ComparisonCoefficientDivisionEqualsTest : BaseTestWithRandomValues<Func<float, (bool, bool)>>
{
	// ReSharper disable CompareOfFloatsByEqualityOperator — testing exactly that comparison.
	public override string TestMethod => GetString(x => (x * 6 == 1, x * 6 != 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x == 0.16666667f, x != 0.16666667f)),
	];
}