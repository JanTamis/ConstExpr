namespace ConstExpr.Tests.Rewriter;

/// <summary>x ^ 0 = x (identity element).</summary>
[InheritsTests]
public class XorIdentityTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x ^ 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
		Create(_ => 5, [ 5 ]),
		Create(_ => -3, [ -3 ])
	];
}