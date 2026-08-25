namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for bitwise AND self-complement: x &amp; ~x = 0.</summary>
[InheritsTests]
public class AndSelfComplementTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x & ~x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}
