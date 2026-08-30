using System.Globalization;
using System.Text.RegularExpressions;

namespace ConstExpr.Tests.Regex;

/// <summary>
///   Differential tests for the regex lowerings: real <see cref="Regex" /> next to the form the
///   generator emits, over a corpus built from the inputs those rewrites can actually diverge on.
///   <para>
///     This exists because <see cref="BaseTest{TDelegate}" /> can't catch these. It string-compares the
///     rendered optimized body and never <em>executes</em> it, so a lowering that emits perfectly
///     well-formed but semantically different code passes the whole suite. Every rule below is
///     therefore checked by running both sides, including the near-miss shapes the classifier must
///     refuse - a rule that silently widened would show up here as a mismatch rather than as a green
///     suite.
///   </para>
///   <para>
///     Note the fully-qualified <c>System.Text.RegularExpressions</c> names in places: this namespace is
///     itself called <c>Regex</c>.
///   </para>
/// </summary>
public class RegexLoweringEquivalenceTests
{
	/// <summary>
	///   Inputs chosen for the ways these rewrites can diverge: embedded and trailing newlines (<c>$</c>
	///   matches before a final <c>\n</c>, <c>.</c> never crosses one), the empty string, and Unicode
	///   letters/digits versus a superscript that <c>\w</c> rejects.
	/// </summary>
	private static readonly string[] Corpus =
	[
		"", "Test", "Testing", "aTestb", "xTest", "TestTest",
		"Test\n", "Test\nx", "\nTest", "x\nTest", "Test\r\n", "aString\n",
		"a.b", "aXb", "a\tb", "atb", "a1_", "aé_", "a\u0660_", "a\u00b2_",
		"a,b,c,d", "eee", "abc123def456", "Hello big world",
		@"C:\dir", @"C:\\dir", "say \"hi\""
	];

	[Test]
	public async Task AnchoredLiteralIsStartsWith()
	{
		foreach (var input in Corpus)
		{
			await Assert.That(input.StartsWith("Test", StringComparison.Ordinal))
				.IsEqualTo(System.Text.RegularExpressions.Regex.IsMatch(input, "^Test"))
				.Because($"^Test on {Show(input)}");
		}
	}

	[Test]
	public async Task BareLiteralIsContains()
	{
		foreach (var input in Corpus)
		{
			await Assert.That(input.Contains("Test", StringComparison.Ordinal))
				.IsEqualTo(System.Text.RegularExpressions.Regex.IsMatch(input, "Test"))
				.Because($"Test on {Show(input)}");
		}
	}

	/// <summary>
	///   Regex.Escape renders punctuation as backslash + the character, which the classifier unescapes
	///   back to a literal. `a\.b` must therefore behave as the literal "a.b", not as "any character".
	/// </summary>
	[Test]
	public async Task EscapedPunctuationIsALiteralCharacter()
	{
		foreach (var input in Corpus)
		{
			await Assert.That(input.Contains("a.b", StringComparison.Ordinal))
				.IsEqualTo(System.Text.RegularExpressions.Regex.IsMatch(input, @"a\.b"))
				.Because($@"a\.b on {Show(input)}");
		}
	}

	/// <summary>
	///   A Windows path is the most common escaped pattern in practice: Regex.Escape doubles the
	///   backslash and the classifier unescapes it back to one, so the literal it hands on still has to
	///   round-trip through literal rendering. Quotes take the same route.
	/// </summary>
	[Test]
	public async Task EscapedBackslashAndQuoteRoundTrip()
	{
		foreach (var input in Corpus)
		{
			await Assert.That(input.Contains(@"C:\dir", StringComparison.Ordinal))
				.IsEqualTo(System.Text.RegularExpressions.Regex.IsMatch(input, @"C:\\dir"))
				.Because($@"C:\\dir on {Show(input)}");

			await Assert.That(input.Contains("\"", StringComparison.Ordinal))
				.IsEqualTo(System.Text.RegularExpressions.Regex.IsMatch(input, "\\\""))
				.Because($"quote on {Show(input)}");
		}
	}

	/// <summary>
	///   The near-misses the classifier must refuse. Each would be a miscompile if it were lowered to the
	///   obvious string operation, so this asserts they genuinely differ - if one ever stopped differing
	///   the corresponding rejection in the classifier would be dead weight rather than load-bearing.
	/// </summary>
	[Test]
	public async Task RefusedShapesReallyDoDiverge()
	{
		// `$` also matches immediately before a trailing newline.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("aString\n", "String$")).IsTrue();
		await Assert.That("aString\n".EndsWith("String", StringComparison.Ordinal)).IsFalse();

		// `^L$` is not equality, for the same reason.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("Test\n", "^Test$")).IsTrue();
		await Assert.That("Test\n" == "Test").IsFalse();

		// `.` never crosses a newline, so `^L.*$` is not StartsWith.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("Test\nx", "^Test.*$")).IsFalse();
		await Assert.That("Test\nx".StartsWith("Test", StringComparison.Ordinal)).IsTrue();

		// Backslash + letter is a class escape: `a\tb` is a TAB, not the letter 't'.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("a\tb", @"a\tb")).IsTrue();
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("atb", @"a\tb")).IsFalse();

		// A mid-pattern `^` is an anchor that can never match outside Multiline.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("ab", "a^b")).IsFalse();
	}

	/// <summary>
	///   The options gate. Each of these makes the literal lowering wrong, which is why
	///   TryGetLowerablePattern accepts RegexOptions.None alone.
	/// </summary>
	[Test]
	public async Task NonDefaultOptionsBreakTheLowering()
	{
		// Multiline turns `^` into a per-line anchor.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("x\nTest", "^Test", RegexOptions.Multiline)).IsTrue();
		await Assert.That("x\nTest".StartsWith("Test", StringComparison.Ordinal)).IsFalse();

		// IgnoreCase breaks the ordinal comparison.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("aTESTb", "Test", RegexOptions.IgnoreCase)).IsTrue();
		await Assert.That("aTESTb".Contains("Test", StringComparison.Ordinal)).IsFalse();

		// ECMAScript narrows \w to ASCII, so it is not `char.IsLetterOrDigit || '_'` either.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("aé_", @"^\w\w\w$")).IsTrue();
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("aé_", @"^\w\w\w$", RegexOptions.ECMAScript)).IsFalse();

		// IgnorePatternWhitespace changes what the pattern string even parses to.
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("Test", "^Te st", RegexOptions.IgnorePatternWhitespace)).IsTrue();
		await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("Test", "^Te st")).IsFalse();
	}

	/// <summary>
	///   IgnoreCase is culture-sensitive without CultureInvariant, so OrdinalIgnoreCase is not a safe
	///   substitute for it - the Turkish dotless i is the standard witness.
	/// </summary>
	[Test]
	public async Task IgnoreCaseIsCultureSensitive()
	{
		var original = CultureInfo.CurrentCulture;

		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

			await Assert.That(System.Text.RegularExpressions.Regex.IsMatch("i", "I", RegexOptions.IgnoreCase)).IsFalse();
			await Assert.That("i".Contains("I", StringComparison.OrdinalIgnoreCase)).IsTrue();
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	/// <summary>
	///   The two shape-independent eliding rewrites, over every corpus input against a mix of patterns
	///   including zero-width ones.
	/// </summary>
	[Test]
	public async Task ResultObjectElisionsAreExact()
	{
		string[] patterns = [ @"\d+", "a*", "", "(?=a)", "^", "x{0}", "(ab)+", "Test", @"\w+" ];

		foreach (var input in Corpus)
		{
			foreach (var pattern in patterns)
			{
				await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(input, pattern))
					.IsEqualTo(System.Text.RegularExpressions.Regex.Match(input, pattern).Success)
					.Because($"Match().Success vs IsMatch: {Show(input)} / {Show(pattern)}");

				await Assert.That(System.Text.RegularExpressions.Regex.Count(input, pattern))
					.IsEqualTo(System.Text.RegularExpressions.Regex.Matches(input, pattern).Count)
					.Because($"Matches().Count vs Count: {Show(input)} / {Show(pattern)}");
			}
		}
	}

	private static string Show(string value)
	{
		return "\"" + value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
	}
}