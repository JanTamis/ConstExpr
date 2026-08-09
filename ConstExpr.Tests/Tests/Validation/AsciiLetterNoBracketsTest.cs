namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterNoBracketsTest : BaseTest<Func<char, bool>>
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiLetter(c)),
		CreateFolded('m'),
		CreateFolded('M'),
		CreateFolded('5')
	];
}