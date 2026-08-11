namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfPlusMinusOneMinusTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
	];
}