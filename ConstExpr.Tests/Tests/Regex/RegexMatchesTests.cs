namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexMatchesTests : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Matches(input, pattern).Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("return Regex_ab3uPQ.Matches(input).Count;", Unknown, @"^\d+$"),
	];
}