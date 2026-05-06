namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexMatchTests() : BaseTest<Func<string, string, string>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Match(input, pattern).Value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return Regex_ab3uPQ.Match(input).Value;", Unknown, @"^\d+$"),
		Create("return Regex_ab3uPQ.Match(\"1234\").Value;", "1234", @"^\d+$"),
	];
}