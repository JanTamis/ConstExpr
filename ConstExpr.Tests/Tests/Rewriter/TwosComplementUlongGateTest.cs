namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TwosComplementUlongGateTest : BaseTestWithRandomValues<Func<ulong, ulong>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => ~(n - 1UL))
	];
}