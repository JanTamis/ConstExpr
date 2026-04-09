using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Tests that (c >= '0' &amp;&amp; c &lt;= '9') || (c >= 'a' &amp;&amp; c &lt;= 'z') || (c >= 'A' &amp;&amp; c &lt;= 'Z')
/// is collapsed into <c>Char.IsAsciiLetterOrDigit(c)</c>.
/// </summary>
[InheritsTests]
public class IsAsciiLetterOrDigitTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(c =>
		(c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetterOrDigit(c);", Unknown),
		Create("return true;", '4'),
		Create("return true;", 'q'),
		Create("return true;", 'Q'),
		Create("return false;", '!'),
	];
}

