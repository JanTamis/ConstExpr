namespace ConstExpr.Tests.Regex;

/// <summary>
///   `Regex.Matches(input, pattern).Count` collapses to `Regex.Count`, dropping the MatchCollection
///   allocation. The rewrite is shape-independent - it changes which result object is built, never what
///   the engine matches - so it also fires when the pattern is unknown and no field can be hoisted.
/// </summary>
[InheritsTests]
public class RegexMatchesTests : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Matches(input, pattern).Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Pattern unknown: no field to hoist, but the static call still collapses to Count.
		Create("return System.Text.RegularExpressions.Regex.Count(input, pattern);"),

		// Constant pattern: hoisted into a cached Regex field, then Count on that field.
		Create("return Regex_ab3uPQ.Count(input);", Unknown, @"^\d+$")
	];
}