namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello"),
		CreateFolded("WoRlD123"),
		CreateFolded(System.String.Empty)
	];
}