namespace ConstExpr.Tests.Optimization;

/// <summary>
///   A negative coefficient flips the comparison operator when isolated: v * -c OP k becomes
///   v OP' k / -c, where OP' is the reversed relation.
/// </summary>
[InheritsTests]
public class ComparisonCoefficientDivisionNegativeTest : BaseTestWithRandomValues<Func<float, bool>>
{
	public override string TestMethod => GetString(x => x * -6 < 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x > -0.16666667f),
	];
}