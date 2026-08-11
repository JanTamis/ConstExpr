namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for XOR optimizer strategies.</summary>
[InheritsTests]
public class XorSelfCancellationTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x ^ x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}