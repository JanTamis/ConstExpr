namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x - x - x - x - x + 5 collapses to one group with a negative, non-power-of-two coefficient (-3)
///   as the leading output term — the branch combination VisitBlockTests never exercises (its only
///   collapsed group is a positive, power-of-two coefficient).
/// </summary>
[InheritsTests]
public class ReassociationRewriterTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x - x - x - x - x + 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => 0 - x * 3 + 5),
		Create(_ => 5, [ 0 ]),
		Create(_ => -25, [ 10 ]),
		Create(_ => 11, [ -2 ])
	];
}