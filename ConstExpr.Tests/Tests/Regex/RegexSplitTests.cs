namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexSplitTests : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Split(input, pattern).Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}