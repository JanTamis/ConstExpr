namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToLowerCaseTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.ToLower());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}