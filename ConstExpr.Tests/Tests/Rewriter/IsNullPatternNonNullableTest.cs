namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class IsNullPatternNonNullableTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(s => s is null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => false),
		CreateFolded("hello")
	];
}