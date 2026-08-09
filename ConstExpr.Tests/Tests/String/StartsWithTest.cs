namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest : BaseTest<Func<string, string, bool>>
{
	public override string TestMethod => GetString((s, prefix) => s.StartsWith(prefix));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "hel"),
		CreateFolded("world", "foo"),
		CreateFolded(System.String.Empty, System.String.Empty)
	];
}