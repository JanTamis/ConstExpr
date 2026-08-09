namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest : BaseTest<Func<string, string, string>>
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "world"),
		CreateFolded("test", System.String.Empty),
		CreateFolded(System.String.Empty, System.String.Empty)
	];
}