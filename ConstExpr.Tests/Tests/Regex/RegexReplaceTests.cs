namespace ConstExpr.Tests.Regex;

[InheritsTests]
public class RegexReplaceTests() : BaseTest<Func<string, string, string, System.Text.RegularExpressions.RegexOptions, string>>
{
	public override string TestMethod => GetString((input, pattern, replacement, options) =>
	{
		return System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement, options);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown values: body remains unchanged.
		Create(null),

		// 3-argument overload: pattern can be hoisted into cached Regex field.
		Create(null, Unknown, @"^\d+$", Unknown, System.Text.RegularExpressions.RegexOptions.None),

		// Pattern unknown: no optimization.
		Create(null, Unknown, Unknown, Unknown, System.Text.RegularExpressions.RegexOptions.IgnoreCase),

		// Options unknown: no optimization.
		Create(null, Unknown, @"^\d+$", Unknown, Unknown),

		// Fully constant: should fold to literal result.
		Create("return \"hello # #\";", "hello 1 2", @"\d", "#", System.Text.RegularExpressions.RegexOptions.None),
	];
}