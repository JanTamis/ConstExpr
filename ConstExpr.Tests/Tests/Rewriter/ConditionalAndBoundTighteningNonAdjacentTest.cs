namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Bound tightening also fires when the redundant comparison is not directly adjacent in the &amp;&amp;
///   tree: x &gt; 0 &amp;&amp; y &gt; 1 &amp;&amp; x &gt; 5 => y &gt; 1 &amp;&amp; x &gt; 5.
/// </summary>
[InheritsTests]
public class ConditionalAndBoundTighteningNonAdjacentTest : BaseTest<Func<int, int, bool>>
{
	public override string TestMethod => GetString((x, y) => x > 0 && y > 1 && x > 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => y > 1 && x > 5)
	];
}