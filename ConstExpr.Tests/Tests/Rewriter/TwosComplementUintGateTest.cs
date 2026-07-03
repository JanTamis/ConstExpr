namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TwosComplementUintGateTest : BaseTest<Func<uint, uint>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => ~(n - 1U))
	];
}