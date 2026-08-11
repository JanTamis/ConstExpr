namespace ConstExpr.Tests.Validation;

/// <summary>
///   Verifies that the ASCII char range optimizer works when no explicit parentheses are
///   written. Because &amp;&amp; has higher precedence than ||, the parser produces the same AST
///   as the parenthesized versions, so all patterns should still be recognized.
/// </summary>
[InheritsTests]
public class AsciiCharRangeNoBracketsTest : BaseTestWithRandomValues<Func<char, bool>>
{
	// No parentheses — &&-precedence groups identically to the parenthesized form.
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiHexDigit(c)),
	];
}