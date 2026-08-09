namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexCountTests : BaseTest<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Count(input, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Both unknown: body is unchanged
		CreateDefault(),

		// Both constant: fold to integer literal
		CreateFolded("hello world", @"\w+"),
		CreateFolded("123abc", @"^\d+$"),
		CreateFolded("a1b2c3", @"\d")
	];
}