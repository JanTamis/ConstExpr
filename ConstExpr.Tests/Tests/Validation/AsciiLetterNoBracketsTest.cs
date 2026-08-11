namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterNoBracketsTest : BaseTestWithRandomValues<Func<char, bool>>
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiLetter(c)),
	];
}