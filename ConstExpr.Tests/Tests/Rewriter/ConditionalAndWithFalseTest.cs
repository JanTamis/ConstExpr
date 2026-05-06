namespace ConstExpr.Tests.Rewriter;

/// <summary>b &amp;&amp; false = false.</summary>
[InheritsTests]
public class ConditionalAndWithFalseTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return false;"),
		Create("return false;", true),
		Create("return false;", false),
	];
}