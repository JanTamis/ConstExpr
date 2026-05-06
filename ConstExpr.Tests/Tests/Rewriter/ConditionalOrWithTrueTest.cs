namespace ConstExpr.Tests.Rewriter;

/// <summary>b || true = true.</summary>
[InheritsTests]
public class ConditionalOrWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return true;"),
		Create("return true;", true),
		Create("return true;", false),
	];
}