namespace ConstExpr.Tests.String;

/// <summary>s.Substring(0, length) simplifies to s[..length].</summary>
[InheritsTests]
public class StringSubstringFromZeroTest : BaseTestWithRandomValues<Func<string, int, string>>
{
	public override string TestMethod => GetString((s, length) => s.Substring(0, length));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, length) => s[..length]),
	];
}