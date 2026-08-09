namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLastIndexOfTest : BaseTest<Func<string, string, int>>
{
	public override string TestMethod => GetString((s, sub) => s.LastIndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "l"),
		CreateFolded("hello", "world"),
		CreateFolded("hello", "h"),
		CreateFolded("hello", "o")
	];
}