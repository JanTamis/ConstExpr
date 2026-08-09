namespace ConstExpr.Tests.String;

/// <summary>s.Substring(start) converts to s[start..] range syntax.</summary>
[InheritsTests]
public class StringSubstringFromStartTest : BaseTest<Func<string, int, string>>
{
	public override string TestMethod => GetString((s, start) => s.Substring(start));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, start) => s[start..]),
		CreateFolded("hello", 2)
	];
}