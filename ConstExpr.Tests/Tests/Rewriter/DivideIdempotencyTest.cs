namespace ConstExpr.Tests.Rewriter;

/// <summary>x / x = 1 when x != 0 (idempotency).</summary>
[InheritsTests]
public class DivideIdempotencyTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x / x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 1),
		Create(_ => 1, [ 5 ]),
		Create(_ => 1, [ -3 ])
	];
}