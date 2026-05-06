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