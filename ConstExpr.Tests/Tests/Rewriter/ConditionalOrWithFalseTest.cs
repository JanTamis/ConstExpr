namespace ConstExpr.Tests.Rewriter;

/// <summary>b || false = b.</summary>
[InheritsTests]
public class ConditionalOrWithFalseTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return b;"),
		Create("return true;", true),
		Create("return false;", false),
	];
}