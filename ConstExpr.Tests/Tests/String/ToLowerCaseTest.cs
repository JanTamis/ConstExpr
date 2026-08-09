namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToLowerCaseTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.ToLower());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("HELLO"),
		CreateFolded("WoRlD123"),
		CreateFolded(System.String.Empty)
	];
}