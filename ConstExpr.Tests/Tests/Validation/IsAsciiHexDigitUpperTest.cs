namespace ConstExpr.Tests.Validation;

/// <summary>
///   Tests that (c >= '0' &amp;&amp; c &lt;= '9') || (c >= 'A' &amp;&amp; c &lt;= 'F')
///   is collapsed into <c>Char.IsAsciiHexDigitUpper(c)</c>.
/// </summary>
[InheritsTests]
public class IsAsciiHexDigitUpperTest : BaseTestWithRandomValues<Func<char, bool>>
{
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'A' && c <= 'F');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiHexDigitUpper(c)),
	];
}