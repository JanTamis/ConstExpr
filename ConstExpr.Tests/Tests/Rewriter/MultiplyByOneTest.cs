namespace ConstExpr.Tests.Rewriter;

/// <summary>x * 1 = x.</summary>
[InheritsTests]
public class MultiplyByOneTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
	];
}