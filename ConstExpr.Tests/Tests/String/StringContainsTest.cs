namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringContainsTest : BaseTestWithRandomValues<Func<string, string, bool>>
{
	public override string TestMethod => GetString((s, sub) => s.Contains(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Contains_57jrtQ(s, sub);"),
	];
}