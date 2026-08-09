namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceNullableTest : BaseTest<Func<string?, string, string>>
{
	public override string TestMethod => GetString((a, b) => a ?? b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "world"),
		CreateFolded(null, "world")
	];
}