namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest : BaseTestWithRandomValues<Func<string, string, string>>
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}