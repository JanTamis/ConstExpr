namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringEndsWithCharTest : BaseTestWithRandomValues<Func<string, char, bool>>
{
	public override string TestMethod => GetString((s, c) => s.EndsWith(c));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}