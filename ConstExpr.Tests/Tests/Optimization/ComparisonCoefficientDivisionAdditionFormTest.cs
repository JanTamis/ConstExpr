namespace ConstExpr.Tests.Optimization;

/// <summary>
///   c = 2 never reaches the comparison as a multiply — MultiplyByTwoToAdditionStrategy canonicalizes
///   it to v + v first. The coefficient-division strategy has to recognize that shape too, or the
///   one coefficient every reader expects to see handled (2) would be the one silently left alone.
/// </summary>
[InheritsTests]
public class ComparisonCoefficientDivisionAdditionFormTest : BaseTest<Func<float, bool>>
{
	public override string TestMethod => GetString(x => x * 2 < 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x < 0.5f),
		Create(_ => true, [ 0f ]),
		Create(_ => false, [ 1f ]),
		Create(_ => false, [ 0.5f ])
	];
}