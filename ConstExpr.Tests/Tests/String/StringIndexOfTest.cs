namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIndexOfTest : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((s, sub) => s.IndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}