using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterOrDigitNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetterOrDigit(c);"),
		Create("return true;", '7'),
		Create("return true;", 'x'),
		Create("return true;", 'X'),
		Create("return false;", '@'),
	];
}