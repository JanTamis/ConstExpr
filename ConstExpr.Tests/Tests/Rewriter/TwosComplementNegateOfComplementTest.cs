namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TwosComplementNegateOfComplementTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => -~n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n + 1),
		Create(_ => 6, [ 5 ]), // ~5 = -6, -(-6) = 6
		Create(_ => 1, [ 0 ]) // ~0 = -1, -(-1) = 1
	];
}