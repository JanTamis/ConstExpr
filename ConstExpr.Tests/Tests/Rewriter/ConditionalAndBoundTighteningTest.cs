namespace ConstExpr.Tests.Rewriter;

/// <summary>Bound tightening: x &gt; 0 &amp;&amp; x &gt; 5 => x &gt; 5.</summary>
[InheritsTests]
public class ConditionalAndBoundTighteningTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x > 0 && x > 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x > 5),
		CreateFolded(3),
		CreateFolded(6)
	];
}