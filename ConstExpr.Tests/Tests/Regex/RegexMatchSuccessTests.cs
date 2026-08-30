namespace ConstExpr.Tests.Regex;

/// <summary>
///   `Regex.Match(input, pattern).Success` collapses to `Regex.IsMatch`, dropping the Match allocation.
///   Like the Matches/Count rewrite this needs no constant pattern and no RegexOptions gate: it leaves
///   the pattern, the options and the matching semantics completely untouched.
/// </summary>
[InheritsTests]
public class RegexMatchSuccessTests : BaseTestWithRandomValues<Func<string, string, bool>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Match(input, pattern).Success;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Pattern unknown: no field to hoist, but the static call still collapses to IsMatch.
		Create("return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);"),

		// Constant pattern: hoisted into a cached Regex field, then IsMatch on that field.
		Create("return Regex_ab3uPQ.IsMatch(input);", Unknown, @"^\d+$")
	];
}