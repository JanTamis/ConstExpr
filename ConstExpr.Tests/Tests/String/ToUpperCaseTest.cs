namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}