namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullInequalityNonNullableTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(s => s != null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => true),
		CreateFolded("hello")
	];
}