namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexMatchesTests() : BaseTest<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Matches(input, pattern).Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return Regex_ab3uPQ.Matches(input).Count;", Unknown, @"^\d+$"),
		Create("return Regex_ab3uPQ.Matches(\"1234\").Count;", "1234", @"^\d+$"),
	];
}