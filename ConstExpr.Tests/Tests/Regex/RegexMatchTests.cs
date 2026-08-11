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
		// Match(input, pattern).Value now folds all the way to a literal when input is also known
		// (see ConstExprPartialRewriter.TryFoldRegexPropertyAccess), so this no longer stops at the
		// `Regex_xxx.Match("1234").Value` intermediate shape.
		Create("return \"1234\";", "1234", @"^\d+$")
	];
}