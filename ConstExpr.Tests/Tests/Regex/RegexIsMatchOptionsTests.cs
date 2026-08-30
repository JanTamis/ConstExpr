using System.Text.RegularExpressions;

namespace ConstExpr.Tests.Regex;

/// <summary>
///   The literal-lowering gate: every RegexOptions value other than None changes what the pattern
///   means, so `^Test` may only become StartsWith under None. Under Multiline `^` is a per-line anchor
///   (`^Test` matches "x\nTest"), so the rewrite must fall back to the cached Regex field instead.
///   Note the fully-qualified names: this namespace is itself called `Regex`.
/// </summary>
[InheritsTests]
public class RegexIsMatchOptionsTests : BaseTestWithRandomValues<Func<string, bool>>
{
	public override string TestMethod => GetString(value =>
	{
		return System.Text.RegularExpressions.Regex.IsMatch(value, "^Test", RegexOptions.Multiline);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Regex_C0pVCQ.IsMatch(value);")
	];
}