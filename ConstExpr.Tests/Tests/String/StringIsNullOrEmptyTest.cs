namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrEmptyTest : BaseTest<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => System.String.IsNullOrEmpty(s)),
		CreateFolded(System.String.Empty),
		CreateFolded("hello"),
		CreateFolded("x")
	];
}