namespace ConstExpr.Tests.Rewriter;

/// <summary>b &amp;&amp; b = b (idempotency).</summary>
[InheritsTests]
public class ConditionalAndIdempotencyTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(b => b),
		CreateFolded(true),
		CreateFolded(false)
	];
}