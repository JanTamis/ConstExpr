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
		// Matches(input, pattern).Count now folds all the way to a literal when input is also known
		// (see ConstExprPartialRewriter.TryFoldRegexPropertyAccess), so this no longer stops at the
		// `Regex_xxx.Matches("1234").Count` intermediate shape.
		Create("return 1;", "1234", @"^\d+$")
	];
}