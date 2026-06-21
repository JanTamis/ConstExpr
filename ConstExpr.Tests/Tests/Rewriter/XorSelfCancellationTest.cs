namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for XOR optimizer strategies.</summary>
[InheritsTests]
public class XorSelfCancellationTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x ^ x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
		Create(_ => 0, [ 42 ]),
		Create(_ => 0, [ -7 ])
	];
}