namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringToCharArrayTest : BaseTest<Func<string, char[]>>
{
	public override string TestMethod => GetString(s => s.ToCharArray());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hi"),
		CreateFolded("abc"),
		CreateFolded(System.String.Empty)
	];
}