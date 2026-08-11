namespace ConstExpr.Tests.String;

/// <summary>string.Format with two placeholders is rewritten to an interpolated string.</summary>
[InheritsTests]
public class StringFormatTwoArgsTest : BaseTestWithRandomValues<Func<string, string, string>>
{
	public override string TestMethod => GetString((first, last) => System.String.Format("{0} {1}", first, last));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((first, last) => $"{first} {last}"),
	];
}