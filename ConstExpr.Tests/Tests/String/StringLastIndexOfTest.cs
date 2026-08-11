namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLastIndexOfTest : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((s, sub) => s.LastIndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}