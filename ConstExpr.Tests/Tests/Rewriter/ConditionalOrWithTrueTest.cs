namespace ConstExpr.Tests.Rewriter;

/// <summary>b || true = true.</summary>
[InheritsTests]
public class ConditionalOrWithTrueTest : BaseTestWithRandomValues<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => true),
	];
}