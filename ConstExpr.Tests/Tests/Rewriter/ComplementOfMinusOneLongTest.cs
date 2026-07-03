namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfMinusOneLongTest : BaseTest<Func<long, long>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
		Create(_ => -5L, [ 5L ]),
		Create(_ => 0L, [ 0L ])
	];
}