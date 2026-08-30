namespace ConstExpr.Tests.Regex;

/// <summary>
///   A constant pattern that is really just a literal string - optionally anchored with `^` - lowers to
///   an ordinal string operation, dropping both the regex engine and the hoisted Regex field. Anything
///   the classifier does not explicitly recognise falls back to the cached-field rewrite.
/// </summary>
[InheritsTests]
public class RegexIsMatchTests : BaseTestWithRandomValues<Func<string, string, bool>>
{
	public override string TestMethod => GetString((value, pattern) =>
	{
		return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown pattern: body remains unchanged.
		CreateDefault(),

		// `^literal` is exactly StartsWith - verified against the real engine, "\nTest" included.
		Create("return value.StartsWith(\"Test\", StringComparison.Ordinal);", Unknown, "^Test"),

		// A bare literal is exactly Contains.
		Create("return value.Contains(\"Test\", StringComparison.Ordinal);", Unknown, "Test"),

		// Regex.Escape output stays recognisable: backslash + punctuation is a literal character.
		Create("return value.Contains(\"a.b\", StringComparison.Ordinal);", Unknown, @"a\.b"),

		// The most common real escaped pattern: a Windows path. Regex.Escape doubles the backslash, the
		// classifier unescapes it back to one, and the emitted literal must re-escape it again.
		Create("return value.Contains(\"C:\\\\dir\", StringComparison.Ordinal);", Unknown, @"C:\\dir"),

		// Backslash + letter is a class escape, never a literal: `\t` is a TAB, not the letter 't'.
		// Must fall back rather than lower to Contains("atb").
		Create("return Regex_KVeiog.IsMatch(value);", Unknown, @"a\tb"),

		// `$` also matches immediately before a trailing newline, so `Test$` is NOT EndsWith.
		Create("return Regex_vTVXHw.IsMatch(value);", Unknown, "Test$"),

		// A real pattern is not a literal at all.
		Create("return Regex_ab3uPQ.IsMatch(value);", Unknown, @"^\d+$"),

		// A mid-pattern `^` is an anchor that can never match outside Multiline - not a literal.
		Create("return Regex_xVAw4g.IsMatch(value);", Unknown, "a^b")
	];
}