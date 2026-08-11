namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest : BaseTestWithRandomValues<Func<string, string, bool>>
{
	public override string TestMethod => GetString((s, prefix) => s.StartsWith(prefix));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}