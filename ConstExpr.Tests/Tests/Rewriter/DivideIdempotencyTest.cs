namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x / x = 1 when x != 0 (idempotency). For a symbolic x with no non-zero proof the rewrite must not
///   fire — see DivideIdempotencyNotRewrittenTest and DivideIdempotencyProvenNonZeroTest.
/// </summary>
[InheritsTests]
public class DivideIdempotencyTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x / x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 1)
	];
}