namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexCountTests : BaseTestWithRandomValues<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Count(input, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Both unknown: body is unchanged
		CreateDefault(),
	];
}