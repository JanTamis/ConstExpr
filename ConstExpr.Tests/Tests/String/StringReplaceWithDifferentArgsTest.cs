namespace ConstExpr.Tests.String;

/// <summary>When replacement differs, no optimisation is applied.</summary>
[InheritsTests]
public class StringReplaceWithDifferentArgsTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.Replace("a", "b"));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.Replace('a', 'b')),
	];
}