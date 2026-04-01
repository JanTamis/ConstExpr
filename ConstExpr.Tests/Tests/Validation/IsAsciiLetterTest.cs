using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Tests that (c >= 'a' &amp;&amp; c &lt;= 'z') || (c >= 'A' &amp;&amp; c &lt;= 'Z')
/// is collapsed into <c>Char.IsAsciiLetter(c)</c>.
/// </summary>
[InheritsTests]
public class IsAsciiLetterTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(c =>
		(c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Char.IsAsciiLetter(c);", Unknown),
		Create("return Char.IsAsciiLetter(c);", 'm'),
		Create("return Char.IsAsciiLetter(c);", 'M'),
		Create("return Char.IsAsciiLetter(c);", '5'),
	];
}

