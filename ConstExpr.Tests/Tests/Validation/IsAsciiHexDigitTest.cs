namespace ConstExpr.Tests.Validation;

/// <summary>
///   Tests that the three-range hex-digit check is collapsed into
///   <c>Char.IsAsciiLetterOrDigit(c)</c> by the binary optimizer.
/// </summary>
[InheritsTests]
public class IsAsciiHexDigitTest : BaseTestWithRandomValues<Func<char, bool>>
{
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown char → three-range check collapsed into Char.IsAsciiHexDigit
		Create(c => Char.IsAsciiHexDigit(c)),
	];
}