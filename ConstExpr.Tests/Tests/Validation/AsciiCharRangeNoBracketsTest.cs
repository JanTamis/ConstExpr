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
		Create("return Char.IsAsciiHexDigit(c);"),
		Create("return true;", '5'),
		Create("return true;", 'b'),
		Create("return true;", 'E'),
		Create("return false;", 'z'),
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
		Create("return Char.IsAsciiLetter(c);"),
		Create("return true;", 'm'),
		Create("return true;", 'M'),
		Create("return false;", '5'),
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
		Create("return Char.IsAsciiLetterOrDigit(c);"),
		Create("return true;", '7'),
		Create("return true;", 'x'),
		Create("return true;", 'X'),
		Create("return false;", '@'),
	];
}

