using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetter(c);"),
		Create("return true;", 'm'),
		Create("return true;", 'M'),
		Create("return false;", '5'),
	];
}