namespace ConstExpr.Tests.Tests.Regex;

[InheritsTests]
public class RegexIsMatchTests : BaseTest<Func<string, string, bool>>
{
	public override string TestMethod => GetString((value, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, "^String$"),
	];
}