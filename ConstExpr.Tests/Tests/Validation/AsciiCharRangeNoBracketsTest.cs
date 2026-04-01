using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Verifies that the ASCII char range optimizer works when no explicit parentheses are
/// written. Because &amp;&amp; has higher precedence than ||, the parser produces the same AST
/// as the parenthesized versions, so all patterns should still be recognized.
/// </summary>
[InheritsTests]
public class AsciiCharRangeNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	// No parentheses — &&-precedence groups identically to the parenthesized form.
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiHexDigit(c);", Unknown),
		Create("return Char.IsAsciiHexDigit(c);", '5'),
		Create("return Char.IsAsciiHexDigit(c);", 'b'),
		Create("return Char.IsAsciiHexDigit(c);", 'E'),
		Create("return Char.IsAsciiHexDigit(c);", 'z'),
	];
}

[InheritsTests]
public class AsciiLetterNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetter(c);", Unknown),
		Create("return Char.IsAsciiLetter(c);", 'm'),
		Create("return Char.IsAsciiLetter(c);", 'M'),
		Create("return Char.IsAsciiLetter(c);", '5'),
	];
}

[InheritsTests]
public class AsciiLetterOrDigitNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetterOrDigit(c);", Unknown),
		Create("return Char.IsAsciiLetterOrDigit(c);", '7'),
		Create("return Char.IsAsciiLetterOrDigit(c);", 'x'),
		Create("return Char.IsAsciiLetterOrDigit(c);", 'X'),
		Create("return Char.IsAsciiLetterOrDigit(c);", '@'),
	];
}

