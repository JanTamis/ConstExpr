namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NegateAdditionStrictTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(f => -(5D + f));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(f => -5D - f),
		CreateFolded(10D),
		CreateFolded(0D)
	];
}