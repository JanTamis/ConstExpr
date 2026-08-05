namespace ConstExpr.Tests.Optimization;

/// <summary>
///   A negative divisor flips the comparison operator when isolated: v / -c OP k becomes
///   v OP' k * -c, where OP' is the reversed relation.
/// </summary>
[InheritsTests]
public class ComparisonDivisionNegativeTest : BaseTest<Func<float, bool>>
{
	public override string TestMethod => GetString(x => x / -4 < 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x > -4f),
		Create(_ => true, [ 0f ]),
		Create(_ => false, [ -5f ]),
		Create(_ => true, [ -3f ]),
		Create(_ => false, [ -4f ])
	];
}