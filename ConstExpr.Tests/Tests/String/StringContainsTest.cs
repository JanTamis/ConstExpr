namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringContainsTest : BaseTest<Func<string, string, bool>>
{
	public override string TestMethod => GetString((s, sub) => s.Contains(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Contains_57jrtQ(s, sub);"),
		CreateFolded("hello", "ell"),
		CreateFolded("hello", "world"),
		CreateFolded("abc", System.String.Empty),
		CreateFolded(System.String.Empty, "x")
	];
}