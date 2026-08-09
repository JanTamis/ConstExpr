namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TwosComplementComplementOfNegateTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~-n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n - 1),
		CreateFolded(5), // -5, ~(-5) = 4
		CreateFolded(0) // ~0 = -1
	];
}