namespace ConstExpr.Tests.Rewriter;

// Family 3 (integer): -(C + b) => (-C) - b  — always safe, fires under Strict
[InheritsTests]
public class NegateAdditionIntTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n => -(5 + n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => -5 - n),
	];
}