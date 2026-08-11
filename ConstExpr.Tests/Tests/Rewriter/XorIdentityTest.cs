namespace ConstExpr.Tests.Rewriter;

/// <summary>x ^ 0 = x (identity element).</summary>
[InheritsTests]
public class XorIdentityTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x ^ 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
	];
}