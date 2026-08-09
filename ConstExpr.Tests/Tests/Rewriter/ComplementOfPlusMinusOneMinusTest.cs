namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfPlusMinusOneMinusTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
		CreateFolded(5),
		CreateFolded(0)
	];
}