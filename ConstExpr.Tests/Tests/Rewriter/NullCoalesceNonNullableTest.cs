namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceNonNullableTest : BaseTest<Func<string, string, string>>
{
	public override string TestMethod => GetString((a, b) => a ?? b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, _) => a),
		CreateFolded("hello", "world")
	];
}