namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexIsMatchTests : BaseTestWithRandomValues<Func<string, string, bool>>
{
	public override string TestMethod => GetString((value, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown value - both parameters unknown: body remains unchanged
		CreateDefault(),
	];
}