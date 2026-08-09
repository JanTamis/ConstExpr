namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ComplementOfMinusOneLongTest : BaseTest<Func<long, long>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -n),
		CreateFolded(5L),
		CreateFolded(0L)
	];
}