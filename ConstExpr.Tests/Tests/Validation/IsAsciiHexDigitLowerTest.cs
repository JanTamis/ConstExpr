using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Tests that (c >= '0' &amp;&amp; c &lt;= '9') || (c >= 'a' &amp;&amp; c &lt;= 'f')
/// is collapsed into <c>Char.IsAsciiHexDigitLower(c)</c>.
/// </summary>
[InheritsTests]
public class IsAsciiHexDigitLowerTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(c =>
		(c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiHexDigitLower(c);", Unknown),
		Create("return true;", '3'),
		Create("return true;", 'b'),
		Create("return false;", 'g'),
	];
}

