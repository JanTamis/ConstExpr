namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for bitwise OR self-complement: x | ~x = -1 (all bits set).</summary>
[InheritsTests]
public class OrSelfComplementTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x | ~x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => -1),
	];
}
