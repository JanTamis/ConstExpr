namespace ConstExpr.Tests.Rewriter;

/// <summary>Bound widening: x &lt; 10 || x &lt; 20 => x &lt; 20 (the wider bound subsumes the narrower one).</summary>
[InheritsTests]
public class ConditionalOrBoundWideningTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x < 10 || x < 20);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x < 20)
	];
}