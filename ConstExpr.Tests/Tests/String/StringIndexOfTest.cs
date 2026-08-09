namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIndexOfTest : BaseTest<Func<string, string, int>>
{
	public override string TestMethod => GetString((s, sub) => s.IndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "ell"),
		CreateFolded("hello", "xyz"),
		CreateFolded("hello", "h"),
		CreateFolded("hello", "o")
	];
}