namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringToCharArrayTest : BaseTestWithRandomValues<Func<string, char[]>>
{
	public override string TestMethod => GetString(s => s.ToCharArray());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}