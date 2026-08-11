namespace ConstExpr.Tests.Rewriter;

/// <summary>b || b = b (idempotency).</summary>
[InheritsTests]
public class ConditionalOrIdempotencyTest : BaseTestWithRandomValues<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(b => b),
	];
}