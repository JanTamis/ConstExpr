namespace ConstExpr.Tests.String;

/// <summary>s.Substring(start) converts to s[start..] range syntax.</summary>
[InheritsTests]
public class StringSubstringFromStartTest : BaseTestWithRandomValues<Func<string, int, string>>
{
	// Substring(start) throws unless 0 <= start <= s.Length. Full-range random ints never satisfy that, so
	// every draw threw and was discarded - the random pass checked nothing at all. Capped to 0-3, which the
	// generator's 0-16 character strings clear most of the time.
	protected override int MaxRandomMagnitudeBits => 2;

	// Floor well under the count actually achieved, so a future generator or seed change that silently
	// starves this class again fails loudly instead of quietly checking one case.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString((s, start) => s.Substring(start));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, start) => s[start..]),
	];
}