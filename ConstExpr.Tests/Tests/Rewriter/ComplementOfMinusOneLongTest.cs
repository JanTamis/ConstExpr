namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfMinusOneLongTest : BaseTestWithRandomValues<Func<long, long>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
	];
}