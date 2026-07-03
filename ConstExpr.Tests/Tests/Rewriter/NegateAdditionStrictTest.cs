namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NegateAdditionStrictTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(f => -(5D + f));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(f => -(f + 5D)),
		Create(_ => -15D, [ 10D ]),
		Create(_ => -5D, [ 0D ])
	];
}