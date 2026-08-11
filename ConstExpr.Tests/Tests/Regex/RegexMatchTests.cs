namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexMatchTests : BaseTestWithRandomValues<Func<string, string, string>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Match(input, pattern).Value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("return Regex_ab3uPQ.Match(input).Value;", Unknown, @"^\d+$"),
	];
}