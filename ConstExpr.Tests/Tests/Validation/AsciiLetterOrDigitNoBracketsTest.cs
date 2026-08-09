namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterOrDigitNoBracketsTest : BaseTest<Func<char, bool>>
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiLetterOrDigit(c)),
		CreateFolded('7'),
		CreateFolded('x'),
		CreateFolded('X'),
		CreateFolded('@')
	];
}