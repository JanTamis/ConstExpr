namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexSplitTests() : BaseTest<Func<string, string, int>>
{
	public override string TestMethod => GetString((input, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.Split(input, pattern).Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("return Regex_ab3uPQ.Split(input).Length;", Unknown, @"^\d+$"),
		Create((_, _) => 2, [ "1234-5678", @"-" ]),
	];
}