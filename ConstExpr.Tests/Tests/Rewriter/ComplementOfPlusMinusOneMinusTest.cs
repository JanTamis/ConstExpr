namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfPlusMinusOneMinusTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
		Create(_ => -5, [ 5 ]),
		Create(_ => 0, [ 0 ])
	];
}