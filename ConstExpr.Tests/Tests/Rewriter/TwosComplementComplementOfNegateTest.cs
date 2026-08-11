namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TwosComplementComplementOfNegateTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~-n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n - 1),
	];
}