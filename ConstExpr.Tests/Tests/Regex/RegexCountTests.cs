namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexCountTests() : BaseTest<Func<string, string, int>>
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
		Create((_, _) => 2, [ "hello world", @"\w+" ]),
		Create((_, _) => 0, [ "123abc", @"^\d+$" ]),
		Create((_, _) => 3, [ "a1b2c3", @"\d" ]),
	];
}