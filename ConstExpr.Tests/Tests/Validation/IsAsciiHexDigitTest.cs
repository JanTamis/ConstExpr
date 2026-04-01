using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Tests that the three-range hex-digit check is collapsed into
/// <c>Char.IsAsciiLetterOrDigit(c)</c> by the binary optimizer.
/// </summary>
[InheritsTests]
public class IsAsciiHexDigitTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(c =>
		(c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown char → three-range check collapsed into Char.IsAsciiHexDigit
		Create("return Char.IsAsciiHexDigit(c);", Unknown),
		// Known char constants: char comparisons bypass full constant folding
		// (implicit char→int conversion causes the per-range &&s to fall back to
		// Char.IsAsciiDigit / Char.IsBetween, which are then combined by this optimizer)
		Create("return Char.IsAsciiHexDigit(c);", '5'),
		Create("return Char.IsAsciiHexDigit(c);", 'a'),
		Create("return Char.IsAsciiHexDigit(c);", 'F'),
		Create("return Char.IsAsciiHexDigit(c);", 'g'),
		Create("return Char.IsAsciiHexDigit(c);", 'Z'),
	];
}
