namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrWhiteSpaceNullableTest : BaseTest<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded((object?) null),
		CreateFolded("   "),
		CreateFolded("hello")
	];
}