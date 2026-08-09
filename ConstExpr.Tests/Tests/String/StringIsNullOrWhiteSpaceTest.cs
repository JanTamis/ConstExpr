namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrWhiteSpaceTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.AsSpan().IsWhiteSpace()),
		CreateFolded(System.String.Empty),
		CreateFolded("   "),
		CreateFolded("hello"),
		CreateFolded(" x ")
	];
}