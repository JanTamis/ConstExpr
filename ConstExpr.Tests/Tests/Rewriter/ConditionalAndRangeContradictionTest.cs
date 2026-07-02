namespace ConstExpr.Tests.Rewriter;

/// <summary>Range contradiction: x &gt; 10 &amp;&amp; x &lt; 5 => false (the ranges never overlap).</summary>
[InheritsTests]
public class ConditionalAndRangeContradictionTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x > 10 && x < 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => false)
	];
}