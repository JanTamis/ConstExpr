namespace ConstExpr.Tests.Rewriter;

/// <summary>b &amp;&amp; false = false.</summary>
[InheritsTests]
public class ConditionalAndWithFalseTest : BaseTestWithRandomValues<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => false),
	];
}